using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordEnvelope
    {
        public const int MaxLength = 500;

        public static IReadOnlyList<Paragraph> Build(WordDocu docu, WordBreaks breaks, string size, string recipient, string sender)
        {
            var preset = WordPresets.Pick(WordPresets.Envelopes, size ?? "korean_small", "size", e => e.Label);
            var width = WordPaperSizes.MmToTwips(preset.WidthMm);
            var height = WordPaperSizes.MmToTwips(preset.HeightMm);
            var body = breaks.Body;
            if (WordSections.All(body).Count >= WordSections.MaxSections)
                throw new ToolInputException($"구역은 {WordSections.MaxSections}개까지만 만들 수 있어요.");

            var used = breaks.Used;
            var created = new List<Paragraph>();
            if (!string.IsNullOrWhiteSpace(sender))
                created.Add(Frame(used, sender, 20, WordPaperSizes.MmToTwips(10), WordPaperSizes.MmToTwips(8), Math.Min(WordPaperSizes.MmToTwips(90), width * 45 / 100), WordPaperSizes.MmToTwips(30)));

            if (!string.IsNullOrWhiteSpace(recipient))
                created.Add(Frame(used, recipient, 28, width * 28 / 100, height * 40 / 100, width * 65 / 100, height * 50 / 100));

            var carrier = breaks.NewParagraph();
            var section = new SectionProperties();
            WordPaperSizes.Write(section, width, height, null);
            WordSections.Set(section, new PageMargin { Top = 567, Bottom = 567, Left = 567U, Right = 567U, Header = 283U, Footer = 283U, Gutter = 0U });
            WordSections.Set(section, new Columns { Space = "720" });
            WordSections.Set(section, new DocGrid { LinePitch = 360 });
            carrier.ParagraphProperties = new ParagraphProperties { SectionProperties = section };
            created.Add(carrier);

            var first = body.ChildElements.FirstOrDefault(e => e is Paragraph || e is Table || e is SdtBlock || e is CustomXmlBlock);
            var later = WordSections.Containing(body, first ?? body.Elements<SectionProperties>().Last());
            foreach (var paragraph in created)
            {
                if (first != null)
                    first.InsertBeforeSelf(paragraph);
                else
                    body.Elements<SectionProperties>().Last().InsertBeforeSelf(paragraph);
            }

            WordSections.Set(later, new SectionType { Val = SectionMarkValues.NextPage });
            Quiet(docu, section);
            return created;
        }

        public static void Quiet(WordDocu docu, SectionProperties section)
        {
            foreach (var header in new[] { true, false })
            {
                var root = WordHeaderFooters.Ensure(docu, section, header, HeaderFooterValues.Default);
                WordHeaderFooters.Clear(root, docu, header, WordIds.Collect(docu.Document));
            }
        }

        private static Paragraph Frame(HashSet<string> used, string text, int halfPoints, int x, int y, int width, int height)
        {
            var properties = new ParagraphProperties();
            properties.FrameProperties = new FrameProperties
            {
                Width = width.ToString(CultureInfo.InvariantCulture),
                Height = (uint)height,
                HeightType = HeightRuleValues.Exact,
                HorizontalPosition = HorizontalAnchorValues.Page,
                VerticalPosition = VerticalAnchorValues.Page,
                X = x.ToString(CultureInfo.InvariantCulture),
                Y = y.ToString(CultureInfo.InvariantCulture),
                Wrap = TextWrappingValues.Around,
            };
            properties.SpacingBetweenLines = new SpacingBetweenLines { Before = "0", After = "0", Line = "320", LineRule = LineSpacingRuleValues.Auto };
            properties.Justification = new Justification { Val = JustificationValues.Left };
            var run = new RunProperties(new FontSize { Val = halfPoints.ToString(CultureInfo.InvariantCulture) }, new FontSizeComplexScript { Val = halfPoints.ToString(CultureInfo.InvariantCulture) });
            var paragraph = new Paragraph(properties, WordText.NewRun(WordText.Clean(text).Trim(), run));
            WordIds.Assign(paragraph, used);
            return paragraph;
        }
    }
}
