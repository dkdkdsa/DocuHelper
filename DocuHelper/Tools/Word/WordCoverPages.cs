using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordCoverPages
    {
        public const string Gallery = "Cover Pages";

        private const string Accent = "4472C4";

        public static SdtBlock Find(Body body) =>
            body.Elements<SdtBlock>().FirstOrDefault(s => s.SdtProperties?.GetFirstChild<SdtContentDocPartObject>()?.DocPartGallery?.Val?.Value == Gallery);

        public static SdtBlock Build(WordDocu docu, string design, string title, string subtitle, string author, string date, string summary, HashSet<string> used)
        {
            var preset = WordPresets.Pick(WordPresets.Covers, design ?? "classic", "design", c => c.Label);
            var scan = WordScan.Read(docu.Document);
            var styles = scan.Styles;
            var titleStyle = styles.EnsureStyle(WordKind.Title);
            var subtitleStyle = WordBuiltinStyles.Ensure(styles, "Subtitle");
            var section = scan.Body.Elements<SectionProperties>().LastOrDefault() ?? WordSections.All(scan.Body).First();
            WordSections.TextArea(section, out _, out var height);
            var paragraphs = new List<Paragraph>();
            switch (preset.Key)
            {
                case "banded":
                    paragraphs.Add(Para(used, titleStyle, title, 44, "FFFFFF", true, JustificationValues.Left, 240, 240, null, true));
                    Rest(paragraphs, used, subtitleStyle, subtitle, author, date, summary, 20, 14, JustificationValues.Left, null, null, 360);
                    break;
                case "minimal":
                    paragraphs.Add(Para(used, titleStyle, title, 40, null, true, JustificationValues.Left, height / 4, 240, Border("top", 8), false));
                    Rest(paragraphs, used, subtitleStyle, subtitle, author, date, summary, 16, 12, JustificationValues.Left, "7F7F7F", null, 0);
                    break;
                case "bold":
                    paragraphs.Add(Para(used, titleStyle, title, 72, null, true, JustificationValues.Left, height / 5, 240, Border("bottom", 48), false));
                    Rest(paragraphs, used, subtitleStyle, subtitle, author, date, summary, 12, 12, JustificationValues.Left, null, null, 120);
                    break;
                case "sidebar":
                    paragraphs.Add(Para(used, titleStyle, title, 48, null, true, JustificationValues.Left, height / 5, 240, Border("left", 96), false));
                    Rest(paragraphs, used, subtitleStyle, subtitle, author, date, summary, 20, 14, JustificationValues.Left, null, Border("left", 96), 0);
                    break;
                default:
                    paragraphs.Add(Para(used, titleStyle, title, 56, null, true, JustificationValues.Center, height * 35 / 100, 360, null, false));
                    Rest(paragraphs, used, subtitleStyle, subtitle, author, date, summary, 20, 14, JustificationValues.Center, null, null, height / 5);
                    break;
            }

            var closing = new Paragraph(new Run(new Break { Type = BreakValues.Page }));
            WordIds.Assign(closing, used);
            paragraphs.Add(closing);

            var content = new SdtContentBlock();
            foreach (var paragraph in paragraphs)
                content.AppendChild(paragraph);

            return new SdtBlock(
                new SdtProperties(
                    new SdtId { Val = Random.Shared.Next(100000000, int.MaxValue) },
                    new SdtContentDocPartObject(new DocPartGallery { Val = Gallery }, new DocPartUnique())),
                content);
        }

        public static void Place(WordDocu docu, SdtBlock cover)
        {
            var body = docu.Document.MainDocumentPart.Document.Body;
            var first = body.ChildElements.FirstOrDefault(e => e is Paragraph || e is Table || e is SdtBlock || e is CustomXmlBlock);
            if (first != null)
                first.InsertBeforeSelf(cover);
            else
                body.InsertAt(cover, 0);
        }

        private static void Rest(List<Paragraph> target, HashSet<string> used, string subtitleStyle, string subtitle, string author, string date, string summary,
            int subtitleSize, int restSize, JustificationValues align, string subtitleColor, BorderType border, int authorBefore)
        {
            if (!string.IsNullOrWhiteSpace(subtitle))
                target.Add(Para(used, subtitleStyle, subtitle, subtitleSize, subtitleColor, false, align, 120, 240, border, false));

            var first = true;
            foreach (var text in new[] { author, date })
            {
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                target.Add(Para(used, null, text, restSize, null, false, align, first ? authorBefore : 0, 60, border, false));
                first = false;
            }

            if (!string.IsNullOrWhiteSpace(summary))
                target.Add(Para(used, null, summary, restSize, null, false, align, first ? authorBefore : 240, 0, border, false));
        }

        private static BorderType Border(string side, int size)
        {
            BorderType border = side == "top" ? new TopBorder() : side == "bottom" ? new BottomBorder() : new LeftBorder();
            border.Val = BorderValues.Single;
            border.Size = (uint)size;
            border.Space = (uint)(size >= 48 ? 12 : 6);
            border.Color = Accent;
            return border;
        }

        private static Paragraph Para(HashSet<string> used, string style, string text, int size, string color, bool bold, JustificationValues align, int before, int after, BorderType border, bool band)
        {
            var properties = new ParagraphProperties();
            if (style != null)
                properties.ParagraphStyleId = new ParagraphStyleId { Val = style };

            if (border != null)
            {
                var borders = new ParagraphBorders();
                borders.AppendChild(border.CloneNode(true));
                properties.ParagraphBorders = borders;
            }

            if (band)
                properties.Shading = new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = Accent, ThemeFill = ThemeColorValues.Accent1 };

            properties.SpacingBetweenLines = new SpacingBetweenLines
            {
                Before = Math.Max(0, before).ToString(CultureInfo.InvariantCulture),
                After = after.ToString(CultureInfo.InvariantCulture),
            };

            if (border is LeftBorder)
                properties.Indentation = new Indentation { Left = "240" };

            properties.Justification = new Justification { Val = align };
            var props = new RunProperties();
            if (bold)
            {
                props.AppendChild(new Bold());
                props.AppendChild(new BoldComplexScript());
            }

            if (color != null)
                props.AppendChild(new Color { Val = color });

            props.AppendChild(new FontSize { Val = (size * 2).ToString(CultureInfo.InvariantCulture) });
            props.AppendChild(new FontSizeComplexScript { Val = (size * 2).ToString(CultureInfo.InvariantCulture) });
            var paragraph = new Paragraph(properties, WordText.NewRun(WordText.Clean(text), props));
            WordIds.Assign(paragraph, used);
            return paragraph;
        }
    }
}
