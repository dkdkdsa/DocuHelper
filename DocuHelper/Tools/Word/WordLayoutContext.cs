using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Tools.Word
{
    public sealed class WordLayoutContext
    {
        private const double DefaultFontPt = 10.0;

        public WordLayoutContext(WordprocessingDocument document, WordStyles styles)
        {
            Document = document;
            Styles = styles;
            Theme = WordTheme.FromDocument(document, styles);
        }

        public WordprocessingDocument Document { get; }

        public WordStyles Styles { get; }

        public WordTheme Theme { get; }

        public double NormalSizePt => Styles.NormalFontSize / 2.0;

        public double SizePt(Run run, Paragraph paragraph) => SizeOf(Styles.RunValue<FontSize>(run, paragraph));

        public double MarkSizePt(Paragraph paragraph) => SizeOf(Styles.MarkValue<FontSize>(paragraph));

        public string LatinFamily(Run run, Paragraph paragraph) => FamilyOf(Styles.RunValue<RunFonts>(run, paragraph), false);

        public string EastAsiaFamily(Run run, Paragraph paragraph) => FamilyOf(Styles.RunValue<RunFonts>(run, paragraph), true);

        public string MarkLatinFamily(Paragraph paragraph) => FamilyOf(Styles.MarkValue<RunFonts>(paragraph), false);

        private double SizeOf(FontSize size) =>
            int.TryParse(size?.Val?.Value, out var half) && half > 0 ? half / 2.0 : NormalSizePt > 0 ? NormalSizePt : DefaultFontPt;

        private string FamilyOf(RunFonts fonts, bool eastAsia)
        {
            if (eastAsia)
            {
                if (!string.IsNullOrEmpty(fonts?.EastAsia?.Value))
                    return fonts.EastAsia.Value;

                return fonts?.EastAsiaTheme != null ? Theme.Font(fonts.EastAsiaTheme.Value) : Theme.MinorEastAsian;
            }

            if (!string.IsNullOrEmpty(fonts?.Ascii?.Value))
                return fonts.Ascii.Value;

            if (!string.IsNullOrEmpty(fonts?.HighAnsi?.Value))
                return fonts.HighAnsi.Value;

            return fonts?.AsciiTheme != null ? Theme.Font(fonts.AsciiTheme.Value) : Theme.MinorLatin;
        }
    }
}
