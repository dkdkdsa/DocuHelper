using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed record WordTocOptions(
        string Style,
        string Title,
        int From,
        int To,
        bool PageNumbers,
        bool RightAlign,
        string Leader,
        bool Hyperlinks,
        string Label,
        bool IncludeLabel,
        int Columns);

    public sealed record WordTocResult(IReadOnlyList<OpenXmlElement> Elements, IReadOnlyList<Paragraph> Entries);

    public static class WordTocBuilder
    {
        public const int MaxEntries = 500;
        public const string ContentsKind = "contents";
        public const string FiguresKind = "figures";
        public const string IndexKind = "index";

        private const string TocGallery = "Table of Contents";

        private sealed record Region(string Kind, Paragraph First, Paragraph Last, string Instruction, SdtBlock Sdt);

        public static WordTocOptions Defaults() => new("automatic1", "목차", 1, 3, true, true, "dot", true, "그림", true, 1);

        public static bool Exists(WordDocu docu, string kind) => Regions(docu).Any(r => r.Kind == kind);

        public static WordTocResult Insert(WordDocu docu, WordScan scan, string kind, WordTocOptions options, OpenXmlElement anchor, bool after)
        {
            var used = WordIds.Collect(docu.Document);
            var manual = kind == ContentsKind && options.Style == "manual";
            var entries = Paragraphs(docu, scan, kind, options, manual, Instruction(kind, options), used);
            var elements = new List<OpenXmlElement>();
            if (kind == ContentsKind && !string.IsNullOrWhiteSpace(options.Title))
                elements.Add(Make(scan.Styles, "TOCHeading", new OpenXmlElement[] { TextRun(options.Title.Trim()) }, 0, "none", used));

            if (kind == ContentsKind && !manual)
            {
                var content = new SdtContentBlock(elements.Concat(entries).ToArray());
                elements = new List<OpenXmlElement>
                {
                    new SdtBlock(new SdtProperties(new SdtContentDocPartObject(new DocPartGallery { Val = TocGallery }, new DocPartUnique())), content),
                };
            }
            else
            {
                elements.AddRange(entries);
            }

            Place(anchor, after, elements);
            return new WordTocResult(elements, entries);
        }

        public static string LocationOf(WordDocu docu, WordScan scan, string kind) =>
            WordInline.LocationOf(scan, Regions(docu).FirstOrDefault(r => r.Kind == kind)?.First);

        public static int Update(WordDocu docu, WordScan scan, string kind = null)
        {
            var used = WordIds.Collect(docu.Document);
            var count = 0;
            foreach (var region in Regions(docu).Where(r => kind == null || r.Kind == kind))
            {
                var options = FromInstruction(region.Kind, region.Instruction);
                Replace(region, Paragraphs(docu, scan, region.Kind, options, false, region.Instruction, used));
                count++;
            }

            return count;
        }

        public static int Remove(WordDocu docu, string kind)
        {
            var regions = Regions(docu).Where(r => r.Kind == kind).ToList();
            var anchors = new List<string>();
            foreach (var region in regions)
            {
                var span = Span(region.First, region.Last);
                anchors.AddRange(span.OfType<Paragraph>().SelectMany(p => p.Descendants<Hyperlink>()).Select(h => h.Anchor?.Value)
                    .Where(a => a != null && a.StartsWith(WordBookmarks.TocPrefix, StringComparison.Ordinal)));
                if (region.Sdt != null)
                {
                    if (region.Sdt.Parent != null)
                        region.Sdt.Remove();
                }
                else
                {
                    foreach (var element in span)
                        element.Remove();
                }
            }

            WordBookmarks.DropUnused(docu.Document, anchors);
            return regions.Count;
        }

        public static void Place(OpenXmlElement anchor, bool after, IEnumerable<OpenXmlElement> elements)
        {
            var current = anchor;
            foreach (var element in elements)
            {
                if (after)
                {
                    current.InsertAfterSelf(element);
                    current = element;
                }
                else
                {
                    anchor.InsertBeforeSelf(element);
                }
            }
        }

        private static IReadOnlyList<Region> Regions(WordDocu docu)
        {
            var regions = new List<Region>();
            foreach (var field in WordFields.Read(docu.Document.MainDocumentPart.Document.Body))
            {
                if ((field.Name != "TOC" && field.Name != "INDEX") || field.Simple != null || !field.IsComplete || field.Begin == null || field.End == null)
                    continue;

                var first = field.Begin.Ancestors<Paragraph>().FirstOrDefault();
                var last = field.End.Ancestors<Paragraph>().FirstOrDefault();
                if (first == null || last == null || !ReferenceEquals(first.Parent, last.Parent) || field.Begin.Ancestors<DeletedRun>().Any())
                    continue;

                var kind = field.Name == "INDEX" ? IndexKind : WordFields.Tokenize(field.Instruction).Contains("\\c") ? FiguresKind : ContentsKind;
                var sdt = first.Parent is SdtContentBlock content && content.Parent is SdtBlock block && IsToc(block) ? block : null;
                regions.Add(new Region(kind, first, last, field.Instruction, sdt));
            }

            return regions;
        }

        private static List<Paragraph> Paragraphs(WordDocu docu, WordScan scan, string kind, WordTocOptions options, bool manual, string instruction, HashSet<string> used)
        {
            var list = kind switch
            {
                FiguresKind => Figures(docu, scan, options, used),
                IndexKind => Index(docu, scan, options, used),
                _ => Contents(docu, scan, options, manual, used),
            };
            if (!manual)
                Mark(list, instruction);

            return list;
        }

        private static List<Paragraph> Contents(WordDocu docu, WordScan scan, WordTocOptions options, bool manual, HashSet<string> used)
        {
            var headings = new List<(int Level, WordBlock Block)>();
            foreach (var block in scan.Blocks)
            {
                if (block.IsTable || block.Paragraph == null || block.Text.Trim().Length == 0 || InToc(block.Paragraph))
                    continue;

                var level = OutlineLevel(scan, block);
                if (level >= options.From && level <= options.To)
                    headings.Add((level, block));
            }

            if (headings.Count == 0)
                throw new ToolInputException("목차를 만들 제목이 없어요. 제목 스타일이나 개요 수준을 쓴 문단을 먼저 만들어 주세요.");

            if (headings.Count > MaxEntries)
                throw new ToolInputException($"목차 항목은 {MaxEntries}개까지만 만들 수 있어요.");

            var tab = Tab(scan, options.RightAlign);
            var list = new List<Paragraph>();
            foreach (var (level, block) in headings)
            {
                var text = Clean(WordText.Of(block.Paragraph));
                var anchor = manual ? null : Anchor(docu, block.Paragraph);
                list.Add(Entry(scan.Styles, "TOC" + level.ToString(CultureInfo.InvariantCulture), text, anchor, block.Page, options, !manual, tab, used));
            }

            return list;
        }

        private static List<Paragraph> Figures(WordDocu docu, WordScan scan, WordTocOptions options, HashSet<string> used)
        {
            var tab = Tab(scan, options.RightAlign);
            var list = new List<Paragraph>();
            foreach (var block in scan.Blocks)
            {
                if (block.IsTable || block.Paragraph == null || InToc(block.Paragraph))
                    continue;

                var sequence = WordCaptions.SequenceOf(block.Paragraph);
                if (sequence == null || WordCaptions.LabelOf(sequence) != options.Label)
                    continue;

                var text = options.IncludeLabel ? WordText.Of(block.Paragraph) : AfterNumber(sequence);
                list.Add(Entry(scan.Styles, "TableofFigures", Clean(text), Anchor(docu, block.Paragraph), block.Page, options, true, tab, used));
            }

            if (list.Count == 0)
                throw new ToolInputException($"‘{options.Label}’ 캡션이 없어서 그림 목차를 만들 수 없어요. 캡션을 먼저 넣어 주세요.");

            if (list.Count > MaxEntries)
                throw new ToolInputException($"목차 항목은 {MaxEntries}개까지만 만들 수 있어요.");

            return list;
        }

        private static List<Paragraph> Index(WordDocu docu, WordScan scan, WordTocOptions options, HashSet<string> used)
        {
            var items = WordIndexEntries.Collect(docu, scan);
            if (items.Count == 0)
                throw new ToolInputException("색인을 만들 항목이 없어요. 색인 항목을 먼저 표시해 주세요.");

            var collate = StringComparer.Create(WordIndexEntries.Korean, false);
            var tab = Tab(scan, options.RightAlign);
            var mains = items.GroupBy(i => i.Main)
                .OrderBy(g => WordIndexEntries.Rank(WordIndexEntries.InitialOf(g.Key)))
                .ThenBy(g => WordIndexEntries.InitialOf(g.Key), StringComparer.Ordinal)
                .ThenBy(g => g.Key, collate)
                .ToList();
            var list = new List<Paragraph>();
            string letter = null;
            foreach (var group in mains)
            {
                var initial = WordIndexEntries.InitialOf(group.Key);
                if (initial != letter)
                {
                    letter = initial;
                    list.Add(Make(scan.Styles, "Index1", new OpenXmlElement[] { new Run(new RunProperties(new Bold()), new Text(initial) { Space = SpaceProcessingModeValues.Preserve }) }, 0, "none", used));
                }

                var plain = group.Where(i => i.Sub == null).ToList();
                var see = plain.Select(i => i.See).FirstOrDefault(s => s != null);
                var line = new List<OpenXmlElement> { TextRun(group.Key) };
                var pages = see ?? (plain.Count > 0 ? Pages(plain) : null);
                if (pages != null)
                    line.AddRange(Separated(pages, options.RightAlign));

                list.Add(Make(scan.Styles, "Index1", line, tab, "none", used));
                foreach (var sub in group.Where(i => i.Sub != null).GroupBy(i => i.Sub).OrderBy(s => s.Key, collate))
                {
                    var subLine = new List<OpenXmlElement> { TextRun(sub.Key) };
                    subLine.AddRange(Separated(Pages(sub), options.RightAlign));
                    list.Add(Make(scan.Styles, "Index2", subLine, tab, "none", used));
                }
            }

            if (list.Count > MaxEntries)
                throw new ToolInputException($"목차 항목은 {MaxEntries}개까지만 만들 수 있어요.");

            return list;
        }

        private static Paragraph Entry(WordStyles styles, string style, string text, string anchor, int page, WordTocOptions options, bool field, int tab, HashSet<string> used)
        {
            var linked = field && options.Hyperlinks && anchor != null;
            var body = new List<OpenXmlElement> { TextRun(text) };
            if (options.PageNumbers)
            {
                body.Add(new Run(new TabChar()));
                var number = page.ToString(CultureInfo.InvariantCulture);
                if (field && anchor != null)
                    body.AddRange(WordFields.Complex($"PAGEREF {anchor}{(linked ? " \\h" : string.Empty)}", number, null));
                else
                    body.Add(TextRun(number));
            }

            if (linked)
            {
                var link = new Hyperlink { Anchor = anchor, History = true };
                foreach (var item in body)
                    link.AppendChild(item);

                body = new List<OpenXmlElement> { link };
            }

            return Make(styles, style, body, tab, options.Leader, used);
        }

        private static Paragraph Make(WordStyles styles, string style, IEnumerable<OpenXmlElement> body, int tab, string leader, HashSet<string> used)
        {
            var properties = new ParagraphProperties(new ParagraphStyleId { Val = WordBuiltinStyles.Ensure(styles, style) });
            if (tab > 0)
                properties.AppendChild(new Tabs(new TabStop { Val = TabStopValues.Right, Leader = LeaderOf(leader), Position = tab }));

            var paragraph = new Paragraph(properties);
            foreach (var item in body)
                paragraph.AppendChild(item);

            WordIds.Assign(paragraph, used);
            return paragraph;
        }

        private static void Mark(IReadOnlyList<Paragraph> paragraphs, string instruction)
        {
            if (paragraphs.Count == 0)
                return;

            var first = paragraphs[0];
            var marks = new OpenXmlElement[]
            {
                new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
                new Run(new FieldCode(" " + instruction + " ") { Space = SpaceProcessingModeValues.Preserve }),
                new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            };
            for (var i = 0; i < marks.Length; i++)
                first.InsertAt(marks[i], i + 1);

            paragraphs[^1].AppendChild(new Run(new FieldChar { FieldCharType = FieldCharValues.End }));
        }

        private static void Replace(Region region, IReadOnlyList<Paragraph> paragraphs)
        {
            var span = Span(region.First, region.Last);
            var next = region.Last.NextSibling();
            var parent = region.First.Parent;
            foreach (var paragraph in paragraphs)
            {
                if (next != null)
                    next.InsertBeforeSelf(paragraph);
                else
                    parent.AppendChild(paragraph);
            }

            foreach (var element in span)
                element.Remove();
        }

        private static List<OpenXmlElement> Span(OpenXmlElement first, OpenXmlElement last)
        {
            var list = new List<OpenXmlElement>();
            for (OpenXmlElement element = first; element != null; element = element.NextSibling())
            {
                list.Add(element);
                if (ReferenceEquals(element, last))
                    break;
            }

            return list;
        }

        private static string Anchor(WordDocu docu, Paragraph paragraph)
        {
            var existing = WordBookmarks.InParagraph(paragraph, WordBookmarks.TocPrefix);
            if (existing != null)
                return existing;

            var content = paragraph.ChildElements.Where(c => c is not ParagraphProperties).ToList();
            if (content.Count == 0)
                throw new ToolInputException("목차에 넣을 글이 없는 문단이 있어요.");

            var name = WordBookmarks.NewHidden(docu.Document, WordBookmarks.TocPrefix);
            WordBookmarks.Around(docu, content[0], content[^1], name);
            return name;
        }

        private static int OutlineLevel(WordScan scan, WordBlock block)
        {
            var paragraph = block.Paragraph;
            var direct = paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value;
            if (direct != null)
                return direct.Value < 9 ? direct.Value + 1 : 0;

            foreach (var style in scan.Styles.ParagraphChain(paragraph))
            {
                var level = style.StyleParagraphProperties?.OutlineLevel?.Val?.Value;
                if (level != null)
                    return level.Value < 9 ? level.Value + 1 : 0;
            }

            return block.IsHeading ? WordOutline.LevelOf(scan.Styles, paragraph, block.Kind) : 0;
        }

        private static WordTocOptions FromInstruction(string kind, string instruction)
        {
            var defaults = Defaults();
            var tokens = WordFields.Tokenize(instruction);
            var from = defaults.From;
            var to = defaults.To;
            var label = defaults.Label;
            var columns = defaults.Columns;
            for (var i = 0; i + 1 < tokens.Count; i++)
            {
                if (tokens[i] == "\\o" && TryLevels(tokens[i + 1], out var low, out var high))
                {
                    from = low;
                    to = high;
                }
                else if (tokens[i] == "\\c" && kind == FiguresKind)
                {
                    label = tokens[i + 1];
                }
                else if (tokens[i] == "\\c" && kind == IndexKind && int.TryParse(tokens[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
                {
                    columns = Math.Clamp(count, 1, 4);
                }
            }

            return defaults with { From = from, To = to, Label = label, Columns = columns, Hyperlinks = tokens.Contains("\\h") };
        }

        private static bool TryLevels(string text, out int low, out int high)
        {
            low = 1;
            high = 3;
            var parts = (text ?? string.Empty).Split('-');
            return parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out low)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out high)
                && low >= 1 && high <= 9 && low <= high;
        }

        private static string Instruction(string kind, WordTocOptions options) => kind switch
        {
            FiguresKind => $"TOC \\h \\z \\c \"{options.Label}\"",
            IndexKind => $"INDEX \\e \"\t\" \\c \"{options.Columns}\" \\z \"1042\"",
            _ => $"TOC \\o \"{options.From}-{options.To}\"{(options.Hyperlinks ? " \\h" : string.Empty)} \\z \\u",
        };

        private static string Pages(IEnumerable<WordIndexItem> items) =>
            string.Join(", ", items.Select(i => i.Page).Distinct().OrderBy(p => p).Select(p => p.ToString(CultureInfo.InvariantCulture)));

        private static IEnumerable<OpenXmlElement> Separated(string pages, bool tabbed) =>
            tabbed
                ? new OpenXmlElement[] { new Run(new TabChar()), TextRun(pages) }
                : new OpenXmlElement[] { TextRun(", " + pages) };

        private static string AfterNumber(WordField sequence)
        {
            var builder = new StringBuilder();
            for (var element = WordCaptions.NumberEnd(sequence)?.NextSibling(); element != null; element = element.NextSibling())
                builder.Append(element.InnerText);

            return Clean(builder.ToString());
        }

        private static int Tab(WordScan scan, bool rightAlign)
        {
            if (!rightAlign)
                return 0;

            WordPager.TextArea(scan.Body, out var width, out _);
            return width;
        }

        private static TabStopLeaderCharValues LeaderOf(string value) => value switch
        {
            "hyphen" => TabStopLeaderCharValues.Hyphen,
            "underline" => TabStopLeaderCharValues.Underscore,
            "none" => TabStopLeaderCharValues.None,
            _ => TabStopLeaderCharValues.Dot,
        };

        private static bool IsToc(SdtBlock block) =>
            block.SdtProperties?.GetFirstChild<SdtContentDocPartObject>()?.GetFirstChild<DocPartGallery>()?.Val?.Value == TocGallery;

        private static bool InToc(OpenXmlElement element) => element.Ancestors<SdtBlock>().Any(IsToc);

        private static Run TextRun(string text) => new(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        private static string Clean(string value) => WordText.Clean(value ?? string.Empty).Trim();
    }
}
