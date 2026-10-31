using System.IO;
using System.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpTemplate
    {
        public const string Font = "함초롬바탕";

        private const string HalfPoints = "20";
        private const string SingleLine = "240";
        private const double TopMillimeters = 35;
        private const double BottomMillimeters = 30;
        private const double SideMillimeters = 30;
        private const double HeaderMillimeters = 20;
        private const double FooterMillimeters = 15;

        public static byte[] Blank()
        {
            using var stream = new MemoryStream();
            var blank = WordTemplate.Blank();
            stream.Write(blank, 0, blank.Length);
            stream.Position = 0;

            using (var document = WordprocessingDocument.Open(stream, true))
            {
                var main = document.MainDocumentPart;
                var styles = main.StyleDefinitionsPart.Styles;
                styles.DocDefaults?.Remove();
                styles.InsertAt(Defaults(), 0);
                styles.Save();

                ApplyMargins(main.Document.Body.Elements<SectionProperties>().Last());
                main.Document.Save();
            }

            return stream.ToArray();
        }

        private static DocDefaults Defaults() => new(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = Font, HighAnsi = Font, EastAsia = Font, ComplexScript = Font },
                new Kern { Val = 2U },
                new FontSize { Val = HalfPoints },
                new FontSizeComplexScript { Val = HalfPoints },
                new Languages { Val = "en-US", EastAsia = "ko-KR", Bidi = "ar-SA" })),
            new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                new SpacingBetweenLines { Before = "0", After = "0", Line = SingleLine, LineRule = LineSpacingRuleValues.Auto })));

        private static void ApplyMargins(SectionProperties section)
        {
            var margin = new PageMargin
            {
                Top = HwpUnits.MillimetersToTwips(TopMillimeters),
                Right = (uint)HwpUnits.MillimetersToTwips(SideMillimeters),
                Bottom = HwpUnits.MillimetersToTwips(BottomMillimeters),
                Left = (uint)HwpUnits.MillimetersToTwips(SideMillimeters),
                Header = (uint)HwpUnits.MillimetersToTwips(HeaderMillimeters),
                Footer = (uint)HwpUnits.MillimetersToTwips(FooterMillimeters),
                Gutter = 0U,
            };

            var existing = section.GetFirstChild<PageMargin>();
            if (existing != null)
            {
                existing.InsertAfterSelf(margin);
                existing.Remove();
                return;
            }

            var size = section.GetFirstChild<PageSize>();
            if (size != null)
                size.InsertAfterSelf(margin);
            else
                section.PrependChild(margin);
        }
    }
}
