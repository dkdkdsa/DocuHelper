using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Office2013.Word;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using Cp = DocumentFormat.OpenXml.CustomProperties;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordInventory
    {
        private const int RangeLimit = 2000;
        private const int PaperTolerance = 30;

        public static IReadOnlyList<WordSectionInfo> Sections(WordDocu docu, WordScan scan = null)
        {
            scan ??= WordScan.Read(docu.Document);
            var sections = WordParts.Sections(docu);
            var infos = new List<WordSectionInfo>();
            var previous = 0;
            for (var i = 0; i < sections.Count; i++)
            {
                var last = scan.Blocks.Count;
                if (sections[i].Parent is ParagraphProperties properties && properties.Parent is Paragraph paragraph)
                {
                    var block = scan.Of(paragraph);
                    if (block != null)
                        last = block.Index;
                }

                var first = Math.Min(previous + 1, Math.Max(1, scan.Blocks.Count));
                last = Math.Max(first, Math.Min(last, scan.Blocks.Count));
                var pages = scan.Blocks.Skip(first - 1).Take(last - first + 1).Select(b => b.Page).ToList();
                infos.Add(DescribeSection(sections[i], i + 1, first, last, pages.Count == 0 ? 1 : pages.Min(), pages.Count == 0 ? 1 : pages.Max()));
                previous = last;
            }

            return infos;
        }

        public static IReadOnlyList<WordHeaderFooterInfo> HeadersFooters(WordDocu docu, int? section = null)
        {
            var sections = WordParts.Sections(docu);
            var infos = new List<WordHeaderFooterInfo>();
            for (var number = 1; number <= sections.Count; number++)
            {
                if (section != null && section.Value != number)
                    continue;

                foreach (var kind in new[] { WordAnchors.Header, WordAnchors.Footer })
                {
                    foreach (var variant in new[] { "default", "first", "even" })
                    {
                        var type = variant == "first" ? HeaderFooterValues.First : variant == "even" ? HeaderFooterValues.Even : HeaderFooterValues.Default;
                        var own = StoryRoot(docu, sections[number - 1], kind, type);
                        int? from = null;
                        for (var k = number - 2; own == null && k >= 0; k--)
                        {
                            own = StoryRoot(docu, sections[k], kind, type);
                            if (own != null)
                                from = k + 1;
                        }

                        if (own != null)
                            infos.Add(DescribeStory(own, number, kind, variant, from));
                    }
                }
            }

            return infos;
        }

        public static IReadOnlyList<WordStyleInfo> Styles(WordDocu docu, WordScan scan = null)
        {
            scan ??= WordScan.Read(docu.Document);
            var root = scan.Styles.Root;
            if (root == null)
                return Array.Empty<WordStyleInfo>();

            var users = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var plainParagraphs = 0;
            var plainTables = 0;
            foreach (var part in WordParts.Roots(docu.Document))
            {
                foreach (var element in part.Descendants())
                {
                    var id = element switch
                    {
                        ParagraphStyleId paragraph => paragraph.Val?.Value,
                        RunStyle run => run.Val?.Value,
                        TableStyle table => table.Val?.Value,
                        _ => null,
                    };

                    if (!string.IsNullOrEmpty(id))
                        users[id] = users.TryGetValue(id, out var count) ? count + 1 : 1;
                    else if (element is Paragraph plain && plain.ParagraphProperties?.ParagraphStyleId == null)
                        plainParagraphs++;
                    else if (element is Table table && table.GetFirstChild<TableProperties>()?.TableStyle == null)
                        plainTables++;
                }
            }

            var describer = new WordFormatDescriber(scan);
            var infos = new List<WordStyleInfo>();
            foreach (var type in new[] { StyleValues.Paragraph, StyleValues.Character, StyleValues.Table })
            {
                foreach (var style in root.Elements<Style>().Where(s => s.Type?.Value == type && !string.IsNullOrEmpty(s.StyleId?.Value)))
                {
                    var id = style.StyleId.Value;
                    var basedOn = style.BasedOn?.Val?.Value;
                    infos.Add(new WordStyleInfo
                    {
                        Id = id,
                        Name = style.StyleName?.Val?.Value ?? id,
                        Type = type == StyleValues.Paragraph ? "paragraph" : type == StyleValues.Character ? "character" : "table",
                        BasedOn = basedOn == null ? null : scan.Styles.Find(basedOn)?.StyleName?.Val?.Value ?? basedOn,
                        Users = (users.TryGetValue(id, out var count) ? count : 0)
                            + (style.Default?.Value == true && type == StyleValues.Paragraph ? plainParagraphs : 0)
                            + (style.Default?.Value == true && type == StyleValues.Table ? plainTables : 0),
                        IsDefault = style.Default?.Value == true,
                        IsCustom = style.CustomStyle?.Value == true,
                        Summary = type == StyleValues.Table ? string.Empty : describer.DescribeStyle(style),
                    });
                }
            }

            return infos;
        }

        public static IReadOnlyList<WordCommentInfo> Comments(WordDocu docu, WordScan scan = null)
        {
            var main = docu.Document.MainDocumentPart;
            var comments = main?.WordprocessingCommentsPart?.Comments?.Elements<Comment>().ToList();
            if (comments == null || comments.Count == 0)
                return Array.Empty<WordCommentInfo>();

            scan ??= WordScan.Read(docu.Document);
            var ranges = Ranges(scan.Body);
            var pages = new PageMap(scan);
            var extended = main.WordprocessingCommentsExPart?.CommentsEx?.Elements<CommentEx>().ToList() ?? new List<CommentEx>();
            var lastIds = comments.ToDictionary(c => c, c => WordIds.Of(c.Elements<Paragraph>().LastOrDefault()));
            var infos = new List<WordCommentInfo>();
            foreach (var comment in comments)
            {
                var id = comment.Id?.Value ?? string.Empty;
                var paraId = lastIds[comment];
                var ex = paraId == null ? null : extended.FirstOrDefault(e => string.Equals(e.ParaId?.Value, paraId, StringComparison.OrdinalIgnoreCase));
                string parent = null;
                var parentPara = ex?.ParaIdParent?.Value;
                if (parentPara != null)
                    parent = comments.FirstOrDefault(c => string.Equals(lastIds[c], parentPara, StringComparison.OrdinalIgnoreCase))?.Id?.Value;

                ranges.CommentHosts.TryGetValue(id, out var host);
                ranges.Comments.TryGetValue(id, out var anchored);
                infos.Add(new WordCommentInfo
                {
                    Id = id,
                    Author = comment.Author?.Value,
                    Initials = comment.Initials?.Value,
                    Date = ParseDate(comment.Date?.InnerText),
                    Anchored = Collapse(anchored),
                    Text = string.Join("\n", comment.Descendants<Paragraph>().Select(WordText.Of)).Trim(),
                    Done = ex?.Done?.Value == true,
                    ParentId = parent,
                    ParagraphId = WordIds.Of(host),
                    Page = host == null ? 0 : pages.Of(host),
                });
            }

            return infos;
        }

        public static IReadOnlyList<WordRevisionInfo> Revisions(WordDocu docu, WordScan scan = null)
        {
            scan ??= WordScan.Read(docu.Document);
            var pages = new PageMap(scan);
            var infos = new List<WordRevisionInfo>();
            foreach (var root in WordParts.Roots(docu.Document))
            {
                var where = root switch
                {
                    Document => "body",
                    Header => "header",
                    Footer => "footer",
                    Footnotes => "footnotes",
                    Endnotes => "endnotes",
                    _ => null,
                };

                if (where == null)
                    continue;

                foreach (var element in root.Descendants())
                {
                    var kind = RevisionKind(element, out var detail);
                    if (kind == null)
                        continue;

                    var host = element.Ancestors<Paragraph>().FirstOrDefault();
                    infos.Add(new WordRevisionInfo
                    {
                        Id = Attribute(element, "id") ?? string.Empty,
                        Kind = kind,
                        Detail = detail,
                        Author = Attribute(element, "author"),
                        Date = ParseDate(Attribute(element, "date")),
                        Text = Collapse(RevisionText(element, host)),
                        Where = where,
                        ParagraphId = WordIds.Of(host),
                        Page = where == "body" && host != null ? pages.Of(host) : 0,
                    });
                }
            }

            return infos;
        }

        public static IReadOnlyList<WordNoteInfo> Notes(WordDocu docu, WordScan scan = null) => Notes(docu.Document, scan);

        internal static IReadOnlyList<WordNoteInfo> Notes(DocumentFormat.OpenXml.Packaging.WordprocessingDocument document, WordScan scan)
        {
            var main = document.MainDocumentPart;
            var footnotes = main?.FootnotesPart?.Footnotes?.Elements<Footnote>().Where(WordStats.IsReal).ToList() ?? new List<Footnote>();
            var endnotes = main?.EndnotesPart?.Endnotes?.Elements<Endnote>().Where(WordStats.IsReal).ToList() ?? new List<Endnote>();
            if (footnotes.Count == 0 && endnotes.Count == 0)
                return Array.Empty<WordNoteInfo>();

            scan ??= WordScan.Read(document);
            var pages = new PageMap(scan);
            var footnoteRefs = new List<FootnoteReference>();
            var endnoteRefs = new List<EndnoteReference>();
            foreach (var element in scan.Body.Descendants())
            {
                if (element is FootnoteReference footnote)
                    footnoteRefs.Add(footnote);
                else if (element is EndnoteReference endnote)
                    endnoteRefs.Add(endnote);
            }

            var infos = new List<WordNoteInfo>();
            infos.AddRange(Order(footnotes, footnoteRefs.Select(r => r.Id?.Value), false, pages, footnoteRefs.Select(r => (OpenXmlElement)r).ToList()));
            infos.AddRange(Order(endnotes, endnoteRefs.Select(r => r.Id?.Value), true, pages, endnoteRefs.Select(r => (OpenXmlElement)r).ToList()));
            return infos;
        }

        public static IReadOnlyList<WordBookmarkInfo> Bookmarks(WordDocu docu, WordScan scan = null)
        {
            scan ??= WordScan.Read(docu.Document);
            var starts = scan.Body.Descendants<BookmarkStart>().Where(b => !string.IsNullOrEmpty(b.Name?.Value)).ToList();
            if (starts.Count == 0)
                return Array.Empty<WordBookmarkInfo>();

            var ranges = Ranges(scan.Body);
            var pages = new PageMap(scan);
            var infos = new List<WordBookmarkInfo>();
            foreach (var start in starts)
            {
                var id = start.Id?.Value ?? string.Empty;
                ranges.Bookmarks.TryGetValue(id, out var text);
                var host = start.Ancestors<Paragraph>().FirstOrDefault();
                infos.Add(new WordBookmarkInfo
                {
                    Name = start.Name.Value,
                    Id = id,
                    Text = Collapse(text),
                    ParagraphId = WordIds.Of(host),
                    Page = host == null ? 0 : pages.Of(host),
                });
            }

            return infos;
        }

        public static IReadOnlyList<WordFieldInfo> Fields(WordDocu docu, WordScan scan = null)
        {
            scan ??= WordScan.Read(docu.Document);
            var pages = new PageMap(scan);
            var positions = new Dictionary<FieldChar, int>();
            foreach (var chars in scan.Body.Descendants<FieldChar>())
                positions[chars] = positions.Count;

            var infos = new List<WordFieldInfo>();
            var stack = new Stack<(int End, int Owner)>();
            foreach (var field in WordFields.Read(scan.Body))
            {
                var begin = field.Begin != null && positions.TryGetValue(field.Begin, out var b) ? b : -1;
                var end = field.End != null && positions.TryGetValue(field.End, out var e) ? e : int.MaxValue;
                while (stack.Count > 0 && stack.Peek().End < begin)
                    stack.Pop();

                if (begin >= 0 && stack.Count > 0 && stack.Peek().Owner >= 0)
                {
                    var owner = stack.Peek().Owner;
                    infos[owner].Nested++;
                    stack.Push((end, owner));
                    continue;
                }

                var host = (field.Begin ?? (OpenXmlElement)field.Simple)?.Ancestors<Paragraph>().FirstOrDefault();
                infos.Add(new WordFieldInfo
                {
                    Name = field.Name,
                    Instruction = field.Instruction,
                    Result = Collapse(field.Result),
                    ParagraphId = WordIds.Of(host),
                    Page = host == null ? 0 : pages.Of(host),
                });

                if (begin >= 0)
                    stack.Push((end, field.Name == "TOC" || field.Name == "INDEX" ? infos.Count - 1 : -1));
            }

            return infos;
        }

        public static IReadOnlyList<WordObjectInfo> Objects(WordDocu docu, WordScan scan = null)
        {
            scan ??= WordScan.Read(docu.Document);
            var pages = new PageMap(scan);
            var infos = new List<WordObjectInfo>();
            foreach (var element in scan.Body.Descendants())
            {
                if (element is not Drawing && element is not Picture && element is not EmbeddedObject)
                    continue;

                if (element.Ancestors<AlternateContentFallback>().Any())
                    continue;

                var host = element.Ancestors<Paragraph>().FirstOrDefault();
                var id = WordIds.Of(host);
                var page = host == null ? 0 : pages.Of(host);
                if (element is Drawing drawing)
                    infos.Add(DescribeDrawing(drawing, id, page));
                else if (element is not Picture || !element.Ancestors<EmbeddedObject>().Any())
                    infos.Add(new WordObjectInfo { Kind = element is Picture ? "legacy" : "embedded", ParagraphId = id, Page = page });
            }

            return infos;
        }

        public static IReadOnlyList<WordCaptionInfo> Captions(WordDocu docu, WordScan scan = null)
        {
            scan ??= WordScan.Read(docu.Document);
            var pages = new PageMap(scan);
            var infos = new List<WordCaptionInfo>();
            foreach (var paragraph in scan.Body.Descendants<Paragraph>())
            {
                if (paragraph.Ancestors<AlternateContentFallback>().Any())
                    continue;

                var sequence = FieldsOf(paragraph).FirstOrDefault(f => f.Name == "SEQ");
                var styled = scan.Styles.HasName(paragraph, "caption") || scan.Styles.HasName(paragraph, "캡션");
                if (sequence == null && !styled)
                    continue;

                var tokens = sequence == null ? Array.Empty<string>() : WordFields.Tokenize(sequence.Instruction);
                infos.Add(new WordCaptionInfo
                {
                    Label = tokens.Count > 1 && !tokens[1].StartsWith("\\", StringComparison.Ordinal) ? tokens[1] : string.Empty,
                    Number = (sequence?.Result ?? string.Empty).Trim(),
                    Text = Collapse(WordText.Of(paragraph)),
                    ParagraphId = WordIds.Of(paragraph),
                    Page = pages.Of(paragraph),
                });
            }

            return infos;
        }

        public static IReadOnlyList<WordPropertyInfo> Properties(WordDocu docu)
        {
            var infos = new List<WordPropertyInfo>();
            var document = docu.Document;
            infos.Add(new WordPropertyInfo("settings", "track_changes", WordRevisions.Tracking(docu) ? "on" : "off"));
            var protection = WordProtection.Mode(docu);
            if (protection != WordProtectionMode.None)
                infos.Add(new WordPropertyInfo("settings", "protection", protection.ToString()));

            var core = document.PackageProperties;
            Add(infos, "core", "title", core.Title);
            Add(infos, "core", "subject", core.Subject);
            Add(infos, "core", "author", core.Creator);
            Add(infos, "core", "keywords", core.Keywords);
            Add(infos, "core", "comments", core.Description);
            Add(infos, "core", "category", core.Category);
            Add(infos, "core", "status", core.ContentStatus);
            Add(infos, "core", "language", core.Language);
            Add(infos, "core", "version", core.Version);
            Add(infos, "core", "last_modified_by", core.LastModifiedBy);
            Add(infos, "core", "revision", core.Revision);
            Add(infos, "core", "created", core.Created == null ? null : core.Created.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            Add(infos, "core", "modified", core.Modified == null ? null : core.Modified.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            Add(infos, "core", "last_printed", core.LastPrinted == null ? null : core.LastPrinted.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

            var app = document.ExtendedFilePropertiesPart?.Properties;
            if (app != null)
            {
                Add(infos, "app", "application", app.Application?.Text);
                Add(infos, "app", "app_version", app.ApplicationVersion?.Text);
                Add(infos, "app", "company", app.Company?.Text);
                Add(infos, "app", "manager", app.Manager?.Text);
                Add(infos, "app", "template", app.Template?.Text);
                Add(infos, "app", "pages", app.Pages?.Text);
                Add(infos, "app", "words", app.Words?.Text);
                Add(infos, "app", "characters", app.Characters?.Text);
                Add(infos, "app", "characters_with_spaces", app.CharactersWithSpaces?.Text);
                Add(infos, "app", "lines", app.Lines?.Text);
                Add(infos, "app", "paragraphs", app.Paragraphs?.Text);
                Add(infos, "app", "total_minutes", app.TotalTime?.Text);
            }

            var custom = document.CustomFilePropertiesPart?.Properties;
            if (custom != null)
            {
                foreach (var property in custom.Elements<Cp.CustomDocumentProperty>())
                    Add(infos, "custom", property.Name?.Value, property.InnerText);
            }

            return infos;
        }

        public static string FieldText(Paragraph paragraph)
        {
            var map = WordTextMap.Build(paragraph);
            var spans = map.Spans
                .Where(s => s.Kind != WordSpanKind.Hyperlink)
                .OrderBy(s => s.Start)
                .ThenByDescending(s => s.End)
                .ToList();
            if (spans.Count == 0)
                return map.Text;

            var text = new StringBuilder();
            var cursor = 0;
            foreach (var span in spans)
            {
                if (span.Start < cursor)
                    continue;

                text.Append(map.Text, cursor, span.Start - cursor);
                var name = WordFields.Name(span.Instruction);
                text.Append(name.Length == 0 ? "{?}" : "{" + name + "}");
                cursor = span.End;
            }

            text.Append(map.Text, cursor, map.Length - cursor);
            return text.ToString();
        }

        private static IReadOnlyList<WordField> FieldsOf(Paragraph paragraph) =>
            paragraph.Descendants<FieldChar>().Any() || paragraph.Descendants<SimpleField>().Any() ? WordFields.Read(paragraph) : Array.Empty<WordField>();

        private static WordSectionInfo DescribeSection(SectionProperties section, int number, int first, int last, int firstPage, int lastPage)
        {
            var size = section.GetFirstChild<PageSize>();
            var margin = section.GetFirstChild<PageMargin>();
            var columns = section.GetFirstChild<Columns>();
            var lines = section.GetFirstChild<LineNumberType>();
            var numbers = section.GetFirstChild<PageNumberType>();
            var align = section.GetFirstChild<VerticalTextAlignmentOnPage>()?.Val?.InnerText;
            var width = Number(size?.Width);
            var height = Number(size?.Height);
            var landscape = size?.Orient?.InnerText == "landscape" || (size?.Orient == null && width > height);
            string paper = null;
            if (width != null && height != null)
            {
                foreach (var preset in WordPresets.Sizes.Values)
                {
                    var straight = Math.Abs(preset.WidthTwips - width.Value) <= PaperTolerance && Math.Abs(preset.HeightTwips - height.Value) <= PaperTolerance;
                    var turned = Math.Abs(preset.WidthTwips - height.Value) <= PaperTolerance && Math.Abs(preset.HeightTwips - width.Value) <= PaperTolerance;
                    if (straight || turned)
                    {
                        paper = preset.Key;
                        break;
                    }
                }
            }

            var columnCount = (int)(Number(columns?.ColumnCount) ?? 0);
            if (columnCount <= 0)
                columnCount = Math.Max(1, columns?.Elements<Column>().Count() ?? 1);

            var restart = lines?.Restart?.InnerText;
            return new WordSectionInfo
            {
                Number = number,
                FirstBlock = first,
                LastBlock = last,
                FirstPage = firstPage,
                LastPage = lastPage,
                BreakType = BreakName(section.GetFirstChild<SectionType>()?.Val?.InnerText),
                WidthCm = Cm(width),
                HeightCm = Cm(height),
                Paper = paper,
                Landscape = landscape,
                TopCm = Cm(Number(margin?.Top)),
                BottomCm = Cm(Number(margin?.Bottom)),
                LeftCm = Cm(Number(margin?.Left)),
                RightCm = Cm(Number(margin?.Right)),
                HeaderCm = Cm(Number(margin?.Header)),
                FooterCm = Cm(Number(margin?.Footer)),
                GutterCm = Cm(Number(margin?.Gutter)),
                Columns = columnCount,
                ColumnSpaceCm = Cm(Number(columns?.Space)),
                ColumnLine = columns?.Separator?.InnerText is "1" or "true" or "on",
                ColumnsEqual = columns?.EqualWidth?.InnerText is not ("0" or "false" or "off"),
                LineNumbers = lines == null ? null : $"every {(int)(Number(lines.CountBy) ?? 1)}, restart " + (restart == "newPage" ? "page" : restart == "newSection" ? "section" : "never"),
                PageNumberFormat = numbers?.Format?.InnerText,
                PageNumberStart = Number(numbers?.Start) is double start ? (int)start : null,
                VerticalAlign = align == null || align == "top" ? null : align == "both" ? "justified" : align,
                TitlePage = WordStyles.IsOn(section.GetFirstChild<TitlePage>()),
                PageBorders = section.GetFirstChild<PageBorders>() != null,
            };
        }

        private static string BreakName(string value) => value switch
        {
            "continuous" => "continuous",
            "evenPage" => "even_page",
            "oddPage" => "odd_page",
            "nextColumn" => "next_column",
            _ => "next_page",
        };

        private static OpenXmlElement StoryRoot(WordDocu docu, SectionProperties section, string kind, HeaderFooterValues type) =>
            kind == WordAnchors.Header
                ? WordParts.FindHeader(docu, section, type, false)?.Header
                : WordParts.FindFooter(docu, section, type, false)?.Footer;

        private static WordHeaderFooterInfo DescribeStory(OpenXmlElement root, int section, string kind, string variant, int? from)
        {
            var lines = new List<string>();
            foreach (var paragraph in root.Descendants<Paragraph>().Where(p => !p.Ancestors<AlternateContentFallback>().Any()))
            {
                var text = FieldText(paragraph);
                if (text.Trim().Length > 0)
                    lines.Add(text);
            }

            var watermark = root.Descendants<DocumentFormat.OpenXml.Vml.TextPath>().Select(t => t.String?.Value).FirstOrDefault(t => !string.IsNullOrEmpty(t));
            return new WordHeaderFooterInfo
            {
                Section = section,
                Kind = kind,
                Variant = variant,
                Text = string.Join("\n", lines),
                InheritedFrom = from,
                Watermark = watermark,
            };
        }

        private static WordObjectInfo DescribeDrawing(Drawing drawing, string paragraphId, int page)
        {
            var inline = drawing.GetFirstChild<Wp.Inline>();
            var anchor = drawing.GetFirstChild<Wp.Anchor>();
            OpenXmlElement host = (OpenXmlElement)inline ?? anchor;
            var properties = host?.GetFirstChild<Wp.DocProperties>();
            var extent = inline?.Extent ?? anchor?.Extent;
            var uri = drawing.Descendants<A.GraphicData>().Select(g => g.Uri?.Value).FirstOrDefault(u => u != null) ?? string.Empty;
            string kind;
            if (uri.EndsWith("/picture", StringComparison.Ordinal))
                kind = "picture";
            else if (uri.EndsWith("/chart", StringComparison.Ordinal))
                kind = "chart";
            else if (uri.EndsWith("wordprocessingShape", StringComparison.Ordinal))
                kind = drawing.Descendants<TextBoxContent>().Any() ? "textbox" : "shape";
            else if (uri.EndsWith("wordprocessingGroup", StringComparison.Ordinal))
                kind = "group";
            else if (uri.EndsWith("/diagram", StringComparison.Ordinal))
                kind = "smartart";
            else
                kind = "drawing";

            string wrap = null;
            if (inline != null)
            {
                wrap = "inline";
            }
            else if (anchor != null)
            {
                wrap = anchor.GetFirstChild<Wp.WrapSquare>() != null ? "square"
                    : anchor.GetFirstChild<Wp.WrapTight>() != null ? "tight"
                    : anchor.GetFirstChild<Wp.WrapThrough>() != null ? "through"
                    : anchor.GetFirstChild<Wp.WrapTopBottom>() != null ? "top_bottom"
                    : anchor.BehindDoc?.InnerText is "1" or "true" ? "behind" : "in_front";
            }

            var alt = properties?.Description?.Value;
            if (string.IsNullOrWhiteSpace(alt))
                alt = properties?.Title?.Value;

            return new WordObjectInfo
            {
                Id = Number(properties?.Id) is double docPr ? (int)docPr : null,
                Kind = kind,
                Name = properties?.Name?.Value,
                ParagraphId = paragraphId,
                Page = page,
                WidthCm = Number(extent?.Cx) is double cx ? Math.Round(cx / WordUnits.EmuPerCm, 2) : null,
                HeightCm = Number(extent?.Cy) is double cy ? Math.Round(cy / WordUnits.EmuPerCm, 2) : null,
                Wrap = wrap,
                Alt = string.IsNullOrWhiteSpace(alt) ? null : alt.Trim(),
            };
        }

        private static IEnumerable<WordNoteInfo> Order(IReadOnlyList<FootnoteEndnoteType> notes, IEnumerable<long?> referenceIds, bool endnote, PageMap pages, IReadOnlyList<OpenXmlElement> references)
        {
            var order = referenceIds.ToList();
            var sorted = notes
                .Select(n => (Note: n, Position: order.IndexOf(n.Id?.Value)))
                .OrderBy(x => x.Position < 0 ? int.MaxValue : x.Position)
                .ToList();
            var number = 0;
            foreach (var (note, position) in sorted)
            {
                var host = position < 0 ? null : references[position].Ancestors<Paragraph>().FirstOrDefault();
                yield return new WordNoteInfo
                {
                    IsEndnote = endnote,
                    Id = (int)(note.Id?.Value ?? 0),
                    Number = position < 0 ? 0 : ++number,
                    Text = string.Join("\n", note.Descendants<Paragraph>().Select(WordText.Of)).Trim(),
                    ParagraphId = WordIds.Of(host),
                    Page = host == null ? 0 : pages.Of(host),
                };
            }
        }

        private static string RevisionKind(OpenXmlElement element, out string detail)
        {
            detail = null;
            switch (element.LocalName)
            {
                case "ins" when element is InsertedRun:
                    return "insert";
                case "del" when element is DeletedRun:
                    return "delete";
                case "ins":
                    detail = element.Parent is TableRowProperties ? "table row" : "paragraph mark";
                    return "insert";
                case "del":
                    detail = element.Parent is TableRowProperties ? "table row" : "paragraph mark";
                    return "delete";
                case "moveFrom":
                    detail = "from";
                    return "move";
                case "moveTo":
                    detail = "to";
                    return "move";
                case "rPrChange":
                case "pPrChange":
                case "sectPrChange":
                case "tblPrChange":
                case "tblGridChange":
                case "tcPrChange":
                case "trPrChange":
                case "numberingChange":
                    detail = element.LocalName switch
                    {
                        "rPrChange" => "characters",
                        "pPrChange" => "paragraph",
                        "sectPrChange" => "page setup",
                        "numberingChange" => "numbering",
                        _ => "table",
                    };

                    return "format";
                default:
                    return null;
            }
        }

        private static string RevisionText(OpenXmlElement element, Paragraph host)
        {
            switch (element.LocalName)
            {
                case "ins" when element is InsertedRun:
                    return WordText.Of(element);
                case "del" when element is DeletedRun:
                case "moveFrom":
                case "moveTo":
                    return string.Concat(element.Descendants<OpenXmlElement>().Where(e => e is Text || e is DeletedText).Select(e => e.InnerText));
                case "rPrChange":
                    return element.Parent?.Parent is Run run ? WordText.Of(run) : string.Empty;
                default:
                    return host == null ? string.Empty : WordText.Of(host);
            }
        }

        private static string Attribute(OpenXmlElement element, string name)
        {
            foreach (var attribute in element.GetAttributes())
            {
                if (attribute.LocalName == name && attribute.NamespaceUri == "http://schemas.openxmlformats.org/wordprocessingml/2006/main")
                    return attribute.Value;
            }

            return null;
        }

        private static DateTime? ParseDate(string value) =>
            value != null && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date) ? date : null;

        private static void Add(List<WordPropertyInfo> infos, string group, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                infos.Add(new WordPropertyInfo(group, name, value.Trim()));
        }

        private static double? Cm(double? twips) => twips == null ? null : Math.Round(WordUnits.TwipsToCm(twips.Value), 2);

        private static double? Number(OpenXmlSimpleType value) =>
            value != null && double.TryParse(value.InnerText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

        private static string Collapse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return string.Join(" ", text.Split(new[] { ' ', '\n', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static RangeTexts Ranges(Body body)
        {
            var result = new RangeTexts();
            var openComments = new Dictionary<string, StringBuilder>();
            var openBookmarks = new Dictionary<string, StringBuilder>();
            foreach (var element in body.Descendants())
            {
                switch (element)
                {
                    case CommentRangeStart start when start.Id?.Value != null:
                        openComments[start.Id.Value] = new StringBuilder();
                        result.CommentHosts[start.Id.Value] = element.Ancestors<Paragraph>().FirstOrDefault();
                        break;
                    case CommentRangeEnd end when end.Id?.Value != null:
                        if (openComments.Remove(end.Id.Value, out var comment))
                            result.Comments[end.Id.Value] = comment.ToString();

                        break;
                    case CommentReference reference when reference.Id?.Value != null:
                        if (!result.CommentHosts.ContainsKey(reference.Id.Value))
                            result.CommentHosts[reference.Id.Value] = element.Ancestors<Paragraph>().FirstOrDefault();

                        break;
                    case BookmarkStart bookmark when bookmark.Id?.Value != null:
                        openBookmarks[bookmark.Id.Value] = new StringBuilder();
                        break;
                    case BookmarkEnd end when end.Id?.Value != null:
                        if (openBookmarks.Remove(end.Id.Value, out var text))
                            result.Bookmarks[end.Id.Value] = text.ToString();

                        break;
                    case Text value when openComments.Count + openBookmarks.Count > 0:
                        if (!element.Ancestors<AlternateContentFallback>().Any())
                            Append(openComments, openBookmarks, value.Text);

                        break;
                    case TabChar when openComments.Count + openBookmarks.Count > 0:
                    case Break when openComments.Count + openBookmarks.Count > 0:
                    case Paragraph when openComments.Count + openBookmarks.Count > 0:
                        Append(openComments, openBookmarks, " ");
                        break;
                }
            }

            foreach (var pair in openComments)
                result.Comments[pair.Key] = pair.Value.ToString();

            foreach (var pair in openBookmarks)
                result.Bookmarks[pair.Key] = pair.Value.ToString();

            return result;
        }

        private static void Append(Dictionary<string, StringBuilder> comments, Dictionary<string, StringBuilder> bookmarks, string text)
        {
            foreach (var builder in comments.Values.Concat(bookmarks.Values))
            {
                if (builder.Length < RangeLimit)
                    builder.Append(text);
            }
        }

        private sealed class RangeTexts
        {
            public Dictionary<string, string> Comments { get; } = new();

            public Dictionary<string, string> Bookmarks { get; } = new();

            public Dictionary<string, Paragraph> CommentHosts { get; } = new();
        }

        private sealed class PageMap
        {
            private readonly Dictionary<OpenXmlElement, WordBlock> _blocks = new();

            public PageMap(WordScan scan)
            {
                foreach (var block in scan.Blocks)
                    _blocks[block.Element] = block;
            }

            public int Of(OpenXmlElement element)
            {
                for (var current = element; current != null; current = current.Parent)
                {
                    if (_blocks.TryGetValue(current, out var block))
                        return block.Page;
                }

                return 0;
            }
        }
    }
}
