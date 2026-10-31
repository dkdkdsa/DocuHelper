using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Excel
{
    internal static class ThemeCatalog
    {
        public sealed record ColorSet(string Name, string Dark2, string Light2, string[] Accents, string Hyperlink, string FollowedHyperlink);

        public sealed record FontSet(string Name, string Major, string Minor, string Hangul);

        private static readonly Dictionary<string, ColorSet> s_colors = new(StringComparer.Ordinal)
        {
            ["office"] = new ColorSet("Office 2013 - 2022", "44546A", "E7E6E6", new[] { "4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47" }, "0563C1", "954F72"),
            ["office2007"] = new ColorSet("Office 2007 - 2010", "1F497D", "EEECE1", new[] { "4F81BD", "C0504D", "9BBB59", "8064A2", "4BACC6", "F79646" }, "0000FF", "800080"),
            ["grayscale"] = new ColorSet("Grayscale", "000000", "F8F8F8", new[] { "DDDDDD", "B2B2B2", "969696", "808080", "5F5F5F", "4D4D4D" }, "5F5F5F", "919191"),
            ["blue"] = new ColorSet("Blue", "17406D", "DBEFF9", new[] { "0F6FC6", "009DD9", "0BD0D9", "10CF9B", "7CCA62", "A5C249" }, "F49100", "85DFD0"),
            ["blue_warm"] = new ColorSet("Blue Warm", "242852", "ACCBF9", new[] { "4A66AC", "629DD1", "297FD5", "7F8FA9", "5AA2AE", "9D90A0" }, "9454C3", "3EBBF0"),
            ["green"] = new ColorSet("Green", "455F51", "E3DED1", new[] { "549E39", "8AB833", "C0CF3A", "029676", "4AB5C4", "0989B1" }, "6B9F25", "BA6906"),
            ["orange"] = new ColorSet("Orange", "637052", "CCDDEA", new[] { "E48312", "BD582C", "865640", "9B8357", "C2BC80", "94A088" }, "2998E3", "8C8C8C"),
            ["red"] = new ColorSet("Red", "323232", "E5C243", new[] { "A5300F", "D55816", "E19825", "B19C7D", "7F5F52", "B27D49" }, "6B9F25", "B26B02"),
            ["violet"] = new ColorSet("Violet", "373545", "DCD8DC", new[] { "AD84C6", "8784C7", "5D739A", "6997AF", "84ACB6", "6F8183" }, "69A020", "8C8C8C"),
            ["yellow"] = new ColorSet("Yellow", "39302A", "E5DEDB", new[] { "FFCA08", "F8931D", "CE8D3E", "EC7016", "E64823", "9C6A6A" }, "2998E3", "7F723D"),
        };

        private static readonly Dictionary<string, FontSet> s_fonts = new(StringComparer.Ordinal)
        {
            ["office"] = new FontSet("Office 2013 - 2022", "Calibri Light", "Calibri", "맑은 고딕"),
            ["malgun"] = new FontSet("맑은 고딕", "맑은 고딕", "맑은 고딕", "맑은 고딕"),
            ["gulim"] = new FontSet("굴림", "굴림", "굴림", "굴림"),
            ["dotum"] = new FontSet("돋움", "돋움", "돋움", "돋움"),
            ["batang"] = new FontSet("바탕", "바탕", "바탕", "바탕"),
            ["arial"] = new FontSet("Arial", "Arial", "Arial", "맑은 고딕"),
            ["times"] = new FontSet("Times New Roman", "Times New Roman", "Times New Roman", "바탕"),
        };

        private static readonly Dictionary<string, (string Colors, string Fonts)> s_themes = new(StringComparer.Ordinal)
        {
            ["office"] = ("office", "office"),
            ["office2007"] = ("office2007", "office"),
            ["grayscale"] = ("grayscale", "office"),
            ["blue"] = ("blue", "office"),
            ["green"] = ("green", "office"),
            ["orange"] = ("orange", "office"),
            ["red"] = ("red", "office"),
            ["violet"] = ("violet", "office"),
            ["yellow"] = ("yellow", "office"),
        };
        public static IEnumerable<string> ThemeKeys => s_themes.Keys;

        public static IEnumerable<string> ColorKeys => s_colors.Keys;

        public static IEnumerable<string> FontKeys => s_fonts.Keys;

        public static ColorSet Colors(string key) => key != null && s_colors.TryGetValue(key, out var set) ? set : null;

        public static FontSet Fonts(string key) => key != null && s_fonts.TryGetValue(key, out var set) ? set : null;

        public static (string Colors, string Fonts)? Theme(string key) => key != null && s_themes.TryGetValue(key, out var pair) ? pair : null;

        public static bool Apply(WorkbookPart workbookPart, ColorSet colors, FontSet fonts)
        {
            var themePart = workbookPart.ThemePart ?? DefaultTheme.Add(workbookPart);
            var elements = themePart.Theme.ThemeElements;
            var changed = false;
            if (colors != null)
                changed |= ApplyColors(elements.ColorScheme, colors);

            if (fonts != null)
                changed |= ApplyFonts(elements.FontScheme, fonts);

            return changed;
        }

        private static bool ApplyColors(A.ColorScheme scheme, ColorSet colors)
        {
            var changed = scheme.Name?.Value != colors.Name;
            scheme.Name = colors.Name;
            changed |= Replace(scheme.Dark2Color, colors.Dark2, hex => scheme.Dark2Color = new A.Dark2Color(new A.RgbColorModelHex { Val = hex }));
            changed |= Replace(scheme.Light2Color, colors.Light2, hex => scheme.Light2Color = new A.Light2Color(new A.RgbColorModelHex { Val = hex }));
            changed |= Replace(scheme.Accent1Color, colors.Accents[0], hex => scheme.Accent1Color = new A.Accent1Color(new A.RgbColorModelHex { Val = hex }));
            changed |= Replace(scheme.Accent2Color, colors.Accents[1], hex => scheme.Accent2Color = new A.Accent2Color(new A.RgbColorModelHex { Val = hex }));
            changed |= Replace(scheme.Accent3Color, colors.Accents[2], hex => scheme.Accent3Color = new A.Accent3Color(new A.RgbColorModelHex { Val = hex }));
            changed |= Replace(scheme.Accent4Color, colors.Accents[3], hex => scheme.Accent4Color = new A.Accent4Color(new A.RgbColorModelHex { Val = hex }));
            changed |= Replace(scheme.Accent5Color, colors.Accents[4], hex => scheme.Accent5Color = new A.Accent5Color(new A.RgbColorModelHex { Val = hex }));
            changed |= Replace(scheme.Accent6Color, colors.Accents[5], hex => scheme.Accent6Color = new A.Accent6Color(new A.RgbColorModelHex { Val = hex }));
            changed |= Replace(scheme.Hyperlink, colors.Hyperlink, hex => scheme.Hyperlink = new A.Hyperlink(new A.RgbColorModelHex { Val = hex }));
            changed |= Replace(scheme.FollowedHyperlinkColor, colors.FollowedHyperlink, hex => scheme.FollowedHyperlinkColor = new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = hex }));
            return changed;
        }

        private static bool Replace(A.Color2Type current, string hex, Action<string> set)
        {
            if (string.Equals(current?.RgbColorModelHex?.Val?.Value, hex, StringComparison.OrdinalIgnoreCase))
                return false;

            set(hex);
            return true;
        }

        private static bool ApplyFonts(A.FontScheme scheme, FontSet fonts)
        {
            var changed = scheme.Name?.Value != fonts.Name;
            scheme.Name = fonts.Name;
            changed |= ApplyFont(scheme.MajorFont, fonts.Major, fonts.Hangul);
            changed |= ApplyFont(scheme.MinorFont, fonts.Minor, fonts.Hangul);
            return changed;
        }

        private static bool ApplyFont(A.FontCollectionType collection, string latin, string hangul)
        {
            var changed = false;
            if (collection.LatinFont?.Typeface?.Value != latin)
            {
                collection.LatinFont = new A.LatinFont { Typeface = latin };
                changed = true;
            }

            var hang = collection.Elements<A.SupplementalFont>().FirstOrDefault(f => f.Script?.Value == "Hang");
            if (hang == null)
            {
                collection.Append(new A.SupplementalFont { Script = "Hang", Typeface = hangul });
                return true;
            }

            if (hang.Typeface?.Value != hangul)
            {
                hang.Typeface = hangul;
                changed = true;
            }

            return changed;
        }
    }
}
