using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Office2013.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordParts
    {
        private const string ThemeXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<a:theme xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" name=\"Office\"><a:themeElements>"
            + "<a:clrScheme name=\"Office\"><a:dk1><a:sysClr val=\"windowText\" lastClr=\"000000\"/></a:dk1><a:lt1><a:sysClr val=\"window\" lastClr=\"FFFFFF\"/></a:lt1>"
            + "<a:dk2><a:srgbClr val=\"44546A\"/></a:dk2><a:lt2><a:srgbClr val=\"E7E6E6\"/></a:lt2>"
            + "<a:accent1><a:srgbClr val=\"4472C4\"/></a:accent1><a:accent2><a:srgbClr val=\"ED7D31\"/></a:accent2><a:accent3><a:srgbClr val=\"A5A5A5\"/></a:accent3>"
            + "<a:accent4><a:srgbClr val=\"FFC000\"/></a:accent4><a:accent5><a:srgbClr val=\"5B9BD5\"/></a:accent5><a:accent6><a:srgbClr val=\"70AD47\"/></a:accent6>"
            + "<a:hlink><a:srgbClr val=\"0563C1\"/></a:hlink><a:folHlink><a:srgbClr val=\"954F72\"/></a:folHlink></a:clrScheme>"
            + "<a:fontScheme name=\"Office\"><a:majorFont><a:latin typeface=\"맑은 고딕\"/><a:ea typeface=\"맑은 고딕\"/><a:cs typeface=\"\"/></a:majorFont>"
            + "<a:minorFont><a:latin typeface=\"맑은 고딕\"/><a:ea typeface=\"맑은 고딕\"/><a:cs typeface=\"\"/></a:minorFont></a:fontScheme>"
            + "<a:fmtScheme name=\"Office\"><a:fillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:fillStyleLst>"
            + "<a:lnStyleLst><a:ln w=\"6350\" cap=\"flat\" cmpd=\"sng\" algn=\"ctr\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:prstDash val=\"solid\"/><a:miter lim=\"800000\"/></a:ln>"
            + "<a:ln w=\"12700\" cap=\"flat\" cmpd=\"sng\" algn=\"ctr\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:prstDash val=\"solid\"/><a:miter lim=\"800000\"/></a:ln>"
            + "<a:ln w=\"19050\" cap=\"flat\" cmpd=\"sng\" algn=\"ctr\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:prstDash val=\"solid\"/><a:miter lim=\"800000\"/></a:ln></a:lnStyleLst>"
            + "<a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst>"
            + "<a:bgFillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme>"
            + "</a:themeElements><a:objectDefaults/><a:extraClrSchemeLst/></a:theme>";

        private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        private static readonly HashSet<string> s_annotations = new(StringComparer.Ordinal)
        {
            "ins", "del", "moveFrom", "moveTo", "rPrChange", "pPrChange", "sectPrChange", "tblPrChange", "tblGridChange", "tcPrChange",
            "trPrChange", "numberingChange", "cellIns", "cellDel", "cellMerge", "bookmarkStart", "bookmarkEnd", "commentRangeStart",
            "commentRangeEnd", "commentReference", "comment", "moveFromRangeStart", "moveFromRangeEnd", "moveToRangeStart", "moveToRangeEnd",
        };

        public static Settings EnsureSettings(WordDocu docu) => EnsureSettings(Package(docu));

        public static Settings EnsureSettings(WordprocessingDocument document)
        {
            var main = Main(document);
            var part = main.DocumentSettingsPart ?? main.AddNewPart<DocumentSettingsPart>();
            part.Settings ??= new Settings();
            return part.Settings;
        }

        public static StyleDefinitionsPart EnsureStyles(WordDocu docu)
        {
            var main = Main(Package(docu));
            var part = main.StyleDefinitionsPart ?? main.AddNewPart<StyleDefinitionsPart>();
            part.Styles ??= new Styles();
            return part;
        }

        public static NumberingDefinitionsPart EnsureNumbering(WordDocu docu)
        {
            var main = Main(Package(docu));
            var part = main.NumberingDefinitionsPart ?? main.AddNewPart<NumberingDefinitionsPart>();
            part.Numbering ??= new Numbering();
            return part;
        }

        public static FootnotesPart EnsureFootnotes(WordDocu docu)
        {
            var document = Package(docu);
            var main = Main(document);
            if (main.FootnotesPart != null)
                return main.FootnotesPart;

            var part = main.AddNewPart<FootnotesPart>();
            part.Footnotes = new Footnotes(
                new Footnote(SeparatorParagraph(new SeparatorMark())) { Type = FootnoteEndnoteValues.Separator, Id = -1 },
                new Footnote(SeparatorParagraph(new ContinuationSeparatorMark())) { Type = FootnoteEndnoteValues.ContinuationSeparator, Id = 0 });

            var settings = EnsureSettings(document);
            if (settings.GetFirstChild<FootnoteDocumentWideProperties>() == null)
                settings.AddChild(new FootnoteDocumentWideProperties(new FootnoteSpecialReference { Id = -1 }, new FootnoteSpecialReference { Id = 0 }));

            return part;
        }

        public static EndnotesPart EnsureEndnotes(WordDocu docu)
        {
            var document = Package(docu);
            var main = Main(document);
            if (main.EndnotesPart != null)
                return main.EndnotesPart;

            var part = main.AddNewPart<EndnotesPart>();
            part.Endnotes = new Endnotes(
                new Endnote(SeparatorParagraph(new SeparatorMark())) { Type = FootnoteEndnoteValues.Separator, Id = -1 },
                new Endnote(SeparatorParagraph(new ContinuationSeparatorMark())) { Type = FootnoteEndnoteValues.ContinuationSeparator, Id = 0 });

            var settings = EnsureSettings(document);
            if (settings.GetFirstChild<EndnoteDocumentWideProperties>() == null)
                settings.AddChild(new EndnoteDocumentWideProperties(new EndnoteSpecialReference { Id = -1 }, new EndnoteSpecialReference { Id = 0 }));

            return part;
        }

        public static WordprocessingCommentsPart EnsureComments(WordDocu docu)
        {
            var main = Main(Package(docu));
            var part = main.WordprocessingCommentsPart ?? main.AddNewPart<WordprocessingCommentsPart>();
            part.Comments ??= new Comments();
            return part;
        }

        public static WordprocessingCommentsExPart EnsureCommentsExtended(WordDocu docu)
        {
            var main = Main(Package(docu));
            var part = main.WordprocessingCommentsExPart ?? main.AddNewPart<WordprocessingCommentsExPart>();
            part.CommentsEx ??= new CommentsEx();
            return part;
        }

        public static ThemePart EnsureTheme(WordDocu docu)
        {
            var main = Main(Package(docu));
            if (main.ThemePart != null)
                return main.ThemePart;

            var part = main.AddNewPart<ThemePart>();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ThemeXml));
            part.FeedData(stream);
            return part;
        }

        public static HeaderPart FindHeader(WordDocu docu, SectionProperties section, HeaderFooterValues type, bool inherit = true)
        {
            var document = Package(docu);
            foreach (var candidate in Chain(document, section, inherit))
            {
                var reference = candidate.Elements<HeaderReference>().FirstOrDefault(r => KindOf(r.Type) == type);
                if (reference?.Id?.Value != null && document.MainDocumentPart.TryGetPartById(reference.Id.Value, out var part) && part is HeaderPart header)
                    return header;
            }

            return null;
        }

        public static FooterPart FindFooter(WordDocu docu, SectionProperties section, HeaderFooterValues type, bool inherit = true)
        {
            var document = Package(docu);
            foreach (var candidate in Chain(document, section, inherit))
            {
                var reference = candidate.Elements<FooterReference>().FirstOrDefault(r => KindOf(r.Type) == type);
                if (reference?.Id?.Value != null && document.MainDocumentPart.TryGetPartById(reference.Id.Value, out var part) && part is FooterPart footer)
                    return footer;
            }

            return null;
        }

        public static HeaderPart EnsureHeader(WordDocu docu, SectionProperties section, HeaderFooterValues type)
        {
            var existing = FindHeader(docu, section, type, false);
            if (existing != null)
                return existing;

            var main = Main(Package(docu));
            var part = main.AddNewPart<HeaderPart>();
            part.Header = new Header(StoryParagraph(docu, "Header"));
            section.InsertAt(new HeaderReference { Type = type, Id = main.GetIdOfPart(part) }, ReferenceCount(section));
            return part;
        }

        public static FooterPart EnsureFooter(WordDocu docu, SectionProperties section, HeaderFooterValues type)
        {
            var existing = FindFooter(docu, section, type, false);
            if (existing != null)
                return existing;

            var main = Main(Package(docu));
            var part = main.AddNewPart<FooterPart>();
            part.Footer = new Footer(StoryParagraph(docu, "Footer"));
            section.InsertAt(new FooterReference { Type = type, Id = main.GetIdOfPart(part) }, ReferenceCount(section));
            return part;
        }

        public static IReadOnlyList<SectionProperties> Sections(WordDocu docu)
        {
            var body = Package(docu).MainDocumentPart.Document?.Body;
            return body == null ? new List<SectionProperties>() : body.Descendants<SectionProperties>().ToList();
        }

        public static int NextDrawingId(WordDocu docu)
        {
            var max = 0L;
            foreach (var root in Roots(Package(docu)))
            {
                foreach (var properties in root.Descendants<Wp.DocProperties>())
                {
                    if (properties.Id?.Value > max)
                        max = properties.Id.Value;
                }
            }

            return Checked(max + 1);
        }

        public static int NextBookmarkId(WordDocu docu) => NextAnnotationId(Package(docu));

        public static int NextRevisionId(WordDocu docu) => NextAnnotationId(Package(docu));

        public static int NextRevisionId(WordprocessingDocument document) => NextAnnotationId(document);

        public static string NextCommentId(WordDocu docu) => NextAnnotationId(Package(docu)).ToString(CultureInfo.InvariantCulture);

        public static int NextNoteId(WordDocu docu, bool endnote)
        {
            var main = Main(Package(docu));
            var max = 0L;
            if (endnote)
            {
                foreach (var note in main.EndnotesPart?.Endnotes?.Elements<Endnote>() ?? Enumerable.Empty<Endnote>())
                    max = Math.Max(max, note.Id?.Value ?? 0);
            }
            else
            {
                foreach (var note in main.FootnotesPart?.Footnotes?.Elements<Footnote>() ?? Enumerable.Empty<Footnote>())
                    max = Math.Max(max, note.Id?.Value ?? 0);
            }

            return Checked(max + 1);
        }

        public static IEnumerable<OpenXmlElement> Roots(WordprocessingDocument document)
        {
            var main = document?.MainDocumentPart;
            if (main == null)
                yield break;

            var parts = new List<OpenXmlPart>();
            if (main.Document != null)
                parts.Add(main);

            parts.AddRange(main.HeaderParts);
            parts.AddRange(main.FooterParts);
            if (main.FootnotesPart != null)
                parts.Add(main.FootnotesPart);

            if (main.EndnotesPart != null)
                parts.Add(main.EndnotesPart);

            if (main.WordprocessingCommentsPart != null)
                parts.Add(main.WordprocessingCommentsPart);

            foreach (var part in parts)
            {
                OpenXmlElement root = null;
                try
                {
                    root = part.RootElement;
                }
                catch (Exception)
                {
                }

                if (root != null)
                    yield return root;
            }
        }

        private static WordprocessingDocument Package(WordDocu docu)
        {
            var document = docu?.Document;
            if (document?.MainDocumentPart == null)
                throw new ToolInputException("이 파일은 워드 문서로 열 수 없어요.");

            return document;
        }

        private static MainDocumentPart Main(WordprocessingDocument document) =>
            document?.MainDocumentPart ?? throw new ToolInputException("이 파일은 워드 문서로 열 수 없어요.");

        private static HeaderFooterValues KindOf(EnumValue<HeaderFooterValues> type) => type?.Value ?? HeaderFooterValues.Default;

        private static IEnumerable<SectionProperties> Chain(WordprocessingDocument document, SectionProperties section, bool inherit)
        {
            if (section == null)
                yield break;

            yield return section;
            if (!inherit)
                yield break;

            var all = document.MainDocumentPart.Document?.Body?.Descendants<SectionProperties>().ToList();
            var index = all?.IndexOf(section) ?? -1;
            for (var i = index - 1; i >= 0; i--)
                yield return all[i];
        }

        private static int ReferenceCount(SectionProperties section)
        {
            var count = 0;
            foreach (var child in section.ChildElements)
            {
                if (child is HeaderReference || child is FooterReference)
                    count++;
                else
                    break;
            }

            return count;
        }

        private static Paragraph StoryParagraph(WordDocu docu, string styleName)
        {
            var styleId = WordBuiltinStyles.Ensure(new WordStyles(Package(docu)), styleName);
            return new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
        }

        private static Paragraph SeparatorParagraph(OpenXmlElement mark) =>
            new(
                new ParagraphProperties(new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }),
                new Run(mark));

        private static int NextAnnotationId(WordprocessingDocument document)
        {
            var max = 0L;
            foreach (var root in Roots(document))
            {
                foreach (var element in root.Descendants())
                {
                    if (!s_annotations.Contains(element.LocalName))
                        continue;

                    foreach (var attribute in element.GetAttributes())
                    {
                        if (attribute.LocalName == "id" && attribute.NamespaceUri == WordNamespace
                            && long.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > max)
                            max = number;
                    }
                }
            }

            return Checked(max + 1);
        }

        private static int Checked(long value)
        {
            if (value > int.MaxValue)
                throw new ToolInputException("문서의 번호 공간이 가득 찼어요.");

            return (int)Math.Max(1, value);
        }
    }
}
