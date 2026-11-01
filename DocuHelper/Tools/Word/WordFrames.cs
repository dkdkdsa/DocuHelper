using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocuHelper.Tools.Hwp;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordFrames
    {
        private const double DefaultDistanceTwips = 720;
        private const double DefaultBorderEighthsPt = 4;
        private const string Auto = "000000";
        private const string Separators = "-/[]()|·";

        public static IReadOnlyList<WordPageFrame> Build(WordScan scan, IReadOnlyList<WordSectionLayout> sections, IReadOnlyList<SectionProperties> marks, IReadOnlyList<int> pageSections, int pageCount)
        {
            var document = scan.Document;
            var main = document?.MainDocumentPart;
            if (main == null || sections.Count == 0 || pageCount < 1 || pageSections.Count < pageCount)
                return Array.Empty<WordPageFrame>();

            var evenOdd = main.DocumentSettingsPart?.Settings?.GetFirstChild<EvenAndOddHeaders>() != null;
            var background = HexOf(main.Document?.DocumentBackground?.Color?.Value);
            var notes = WordInventory.Notes(document, scan).Where(n => !n.IsEndnote && n.Page > 0).ToLookup(n => n.Page);
            var pageMarks = PageMarks(scan);
            var firsts = new Dictionary<int, int>();
            for (var page = 1; page <= pageCount; page++)
                firsts.TryAdd(pageSections[page - 1], page);

            var frames = new List<WordPageFrame>(pageCount);
            var number = 0;
            for (var page = 1; page <= pageCount; page++)
            {
                var index = Math.Min(pageSections[page - 1], sections.Count - 1);
                var section = sections[index];
                var mark = index < marks.Count ? marks[index] : null;
                var hidden = Hidden(pageMarks, page);
                number++;
                if (firsts[index] == page && mark?.GetFirstChild<PageNumberType>()?.Start?.Value is int start)
                    number = start;

                if (NewNumber(pageMarks, page) is int restart)
                    number = restart;

                var shown = hidden.Contains('p') ? string.Empty : Format(mark, number);
                var pages = pageCount.ToString(CultureInfo.InvariantCulture);
                var titled = mark?.GetFirstChild<TitlePage>() != null && firsts[index] == page;
                var type = titled ? HeaderFooterValues.First : evenOdd && number % 2 == 0 ? HeaderFooterValues.Even : HeaderFooterValues.Default;
                var headerRoot = hidden.Contains('h') ? null : Story(main, marks, index, type, true);
                var footerRoot = hidden.Contains('f') ? null : Story(main, marks, index, type, false);
                var borders = mark?.GetFirstChild<PageBorders>()?.TopBorder;
                var bordered = !hidden.Contains('b') && borders?.Val != null && borders.Val.Value != BorderValues.None && borders.Val.Value != BorderValues.Nil;
                frames.Add(new WordPageFrame(
                    page,
                    section.Page,
                    section.ColumnCount,
                    section.ColumnGapPt,
                    Distance(mark?.GetFirstChild<PageMargin>()?.Header),
                    Distance(mark?.GetFirstChild<PageMargin>()?.Footer),
                    shown,
                    Lines(headerRoot, shown, pages),
                    Lines(footerRoot, shown, pages),
                    headerRoot == null ? null : WordWatermark.Text(headerRoot),
                    notes[page].Where(n => n.Number > 0).Select(n => new WordNoteLine(n.Number.ToString(CultureInfo.InvariantCulture), n.Text.Replace('\n', ' '))).ToList(),
                    hidden.Contains('g') ? null : background,
                    bordered ? HexOf(borders.Color?.Value) ?? Auto : null,
                    bordered ? (borders.Size?.Value ?? DefaultBorderEighthsPt) / 8.0 : 0));
            }

            return frames;
        }

        private static ILookup<int, string> PageMarks(WordScan scan) =>
            scan.Blocks.Where(b => b.Paragraph != null)
                .SelectMany(b => b.Paragraph.Elements<BookmarkStart>().Select(s => (Page: b.Page, Name: s.Name?.Value ?? string.Empty)))
                .Where(m => m.Name.StartsWith(HwpPageControls.HidePrefix, StringComparison.Ordinal) || m.Name.StartsWith(HwpPageControls.NewNumberPrefix, StringComparison.Ordinal))
                .ToLookup(m => m.Page, m => m.Name);

        private static string Hidden(ILookup<int, string> marks, int page)
        {
            var letters = new StringBuilder();
            foreach (var name in marks[page].Where(n => n.StartsWith(HwpPageControls.HidePrefix, StringComparison.Ordinal)))
                letters.Append(name.Substring(HwpPageControls.HidePrefix.Length));

            return letters.ToString();
        }

        private static int? NewNumber(ILookup<int, string> marks, int page)
        {
            foreach (var name in marks[page].Where(n => n.StartsWith(HwpPageControls.NewNumberPrefix, StringComparison.Ordinal)))
            {
                if (int.TryParse(name.Substring(HwpPageControls.NewNumberPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    return number;
            }

            return null;
        }

        private static string Format(SectionProperties mark, int number) =>
            mark == null ? number.ToString(CultureInfo.InvariantCulture) : WordHeaderFooters.Format(number, WordHeaderFooters.FormatName(mark));

        private static double Distance(UInt32Value value) => (value?.Value ?? DefaultDistanceTwips) / 20.0;

        private static OpenXmlElement Story(MainDocumentPart main, IReadOnlyList<SectionProperties> marks, int index, HeaderFooterValues type, bool header)
        {
            for (var i = index; i >= 0 && i < marks.Count; i--)
            {
                var section = marks[i];
                var id = header
                    ? section.Elements<HeaderReference>().FirstOrDefault(r => KindOf(r.Type) == type)?.Id?.Value
                    : section.Elements<FooterReference>().FirstOrDefault(r => KindOf(r.Type) == type)?.Id?.Value;
                if (id == null)
                    continue;

                if (!main.TryGetPartById(id, out var part))
                    return null;

                return header ? (part as HeaderPart)?.Header : (part as FooterPart)?.Footer;
            }

            return null;
        }

        private static HeaderFooterValues KindOf(EnumValue<HeaderFooterValues> type) => type?.Value ?? HeaderFooterValues.Default;

        private static IReadOnlyList<WordStoryLine> Lines(OpenXmlElement root, string number, string pages)
        {
            if (root == null)
                return Array.Empty<WordStoryLine>();

            return WordScan.BlockElements(root).OfType<Paragraph>()
                .Select(p => Line(p, number, pages))
                .Where(line => line.Text.Trim().Length > 0)
                .ToList();
        }

        private static WordStoryLine Line(Paragraph paragraph, string number, string pages)
        {
            var text = WordText.Of(paragraph);
            var fields = WordFields.Read(paragraph);
            if (number.Length == 0 && IsOnlyNumber(text, fields))
                return new WordStoryLine(string.Empty, "left");

            if (paragraph.Elements<SimpleField>().Any(f => string.IsNullOrEmpty(f.InnerText) && IsNumberInstruction(f.Instruction?.Value)))
                return new WordStoryLine(SimpleText(paragraph, number, pages), text.Contains('\t') ? "tabs" : AlignOf(paragraph.ParagraphProperties?.Justification?.Val?.Value));

            var builder = new StringBuilder();
            var cursor = 0;
            foreach (var field in fields)
            {
                if (string.IsNullOrEmpty(field.Result))
                    continue;

                var at = text.IndexOf(field.Result, cursor, StringComparison.Ordinal);
                if (at < 0)
                    continue;

                builder.Append(text, cursor, at - cursor).Append(FieldValue(field, number, pages));
                cursor = at + field.Result.Length;
            }

            builder.Append(text, cursor, text.Length - cursor);
            return new WordStoryLine(builder.ToString(), text.Contains('\t') ? "tabs" : AlignOf(paragraph.ParagraphProperties?.Justification?.Val?.Value));
        }

        private static bool IsNumberInstruction(string instruction) =>
            (instruction ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant() is "PAGE" or "NUMPAGES";

        private static string SimpleText(Paragraph paragraph, string number, string pages)
        {
            var builder = new StringBuilder();
            foreach (var child in paragraph.ChildElements)
            {
                if (child is SimpleField field && string.IsNullOrEmpty(field.InnerText) && IsNumberInstruction(field.Instruction?.Value))
                {
                    var name = field.Instruction.Value.Trim().Split(' ')[0].ToUpperInvariant();
                    builder.Append(name == "PAGE" ? number : pages);
                    continue;
                }

                foreach (var part in child.Descendants().Where(d => d is Text or TabChar))
                    builder.Append(part is TabChar ? "\t" : ((Text)part).Text);
            }

            return builder.ToString();
        }

        private static bool IsOnlyNumber(string text, IReadOnlyList<WordField> fields)
        {
            var numbered = fields.Where(f => IsNumberField(f) && !string.IsNullOrEmpty(f.Result)).ToList();
            if (numbered.Count == 0)
                return false;

            foreach (var field in numbered)
                text = text.Replace(field.Result, string.Empty);

            return text.All(c => char.IsWhiteSpace(c) || Separators.IndexOf(c) >= 0);
        }

        private static bool IsNumberField(WordField field) => field.Name.ToUpperInvariant() is "PAGE" or "NUMPAGES";

        private static string FieldValue(WordField field, string number, string pages) => field.Name.ToUpperInvariant() switch
        {
            "PAGE" => number,
            "NUMPAGES" => pages,
            _ => field.Result,
        };

        private static string AlignOf(JustificationValues? value)
        {
            if (value == JustificationValues.Center)
                return "center";

            if (value == JustificationValues.Right || value == JustificationValues.End)
                return "right";

            if (value == JustificationValues.Both || value == JustificationValues.Distribute)
                return "justify";

            return "left";
        }

        private static string HexOf(string value) =>
            value != null && value.Length == 6 && value.All(Uri.IsHexDigit) ? value.ToUpperInvariant() : null;
    }
}
