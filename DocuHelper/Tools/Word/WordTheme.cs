using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordTheme
    {
        public static readonly IReadOnlyList<string> ColorKeys = new[]
        {
            "dark1", "light1", "dark2", "light2", "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "hyperlink", "followed_hyperlink",
        };

        private static readonly string[] s_koreanFonts =
        {
            "Malgun", "Batang", "Dotum", "Gulim", "Gungsuh", "Nanum", "HCR", "Hamchorom", "Noto Sans KR", "Noto Serif KR", "Pretendard", "KoPub", "Spoqa",
        };

        public string ColorSchemeName { get; set; } = "Office";

        public string FontSchemeName { get; set; } = "Office";

        public string MajorLatin { get; set; } = WordThemeXml.Fonts;

        public string MajorEastAsian { get; set; } = WordThemeXml.Fonts;

        public string MinorLatin { get; set; } = WordThemeXml.Fonts;

        public string MinorEastAsian { get; set; } = WordThemeXml.Fonts;

        public Dictionary<string, string> Colors { get; set; } = Office();

        public static WordTheme Read(WordprocessingDocument document)
        {
            var theme = document?.MainDocumentPart?.ThemePart?.Theme?.ThemeElements;
            if (theme == null)
                return null;

            var result = new WordTheme();
            var scheme = theme.ColorScheme;
            if (scheme != null)
            {
                result.ColorSchemeName = scheme.Name?.Value ?? "Custom";
                Put(result.Colors, "dark1", scheme.Dark1Color);
                Put(result.Colors, "light1", scheme.Light1Color);
                Put(result.Colors, "dark2", scheme.Dark2Color);
                Put(result.Colors, "light2", scheme.Light2Color);
                Put(result.Colors, "accent1", scheme.Accent1Color);
                Put(result.Colors, "accent2", scheme.Accent2Color);
                Put(result.Colors, "accent3", scheme.Accent3Color);
                Put(result.Colors, "accent4", scheme.Accent4Color);
                Put(result.Colors, "accent5", scheme.Accent5Color);
                Put(result.Colors, "accent6", scheme.Accent6Color);
                Put(result.Colors, "hyperlink", scheme.Hyperlink);
                Put(result.Colors, "followed_hyperlink", scheme.FollowedHyperlinkColor);
            }

            var fonts = theme.FontScheme;
            if (fonts != null)
            {
                result.FontSchemeName = fonts.Name?.Value ?? "Custom";
                result.MajorLatin = Face(fonts.MajorFont?.LatinFont) ?? result.MajorLatin;
                result.MinorLatin = Face(fonts.MinorFont?.LatinFont) ?? result.MinorLatin;
                result.MajorEastAsian = Face(fonts.MajorFont?.EastAsianFont) ?? Script(fonts.MajorFont) ?? result.MajorLatin;
                result.MinorEastAsian = Face(fonts.MinorFont?.EastAsianFont) ?? Script(fonts.MinorFont) ?? result.MinorLatin;
            }

            return result;
        }

        public static Dictionary<string, string> Office()
        {
            var scheme = WordPresets.ColorSchemes["office"];
            return Flatten(scheme);
        }

        public static Dictionary<string, string> Flatten(WordColorScheme scheme)
        {
            var colors = new Dictionary<string, string>
            {
                ["dark1"] = scheme.Dark1,
                ["light1"] = scheme.Light1,
                ["dark2"] = scheme.Dark2,
                ["light2"] = scheme.Light2,
            };

            for (var i = 0; i < scheme.Accents.Count; i++)
                colors["accent" + (i + 1).ToString(CultureInfo.InvariantCulture)] = scheme.Accents[i];

            colors["hyperlink"] = scheme.Hyperlink;
            colors["followed_hyperlink"] = scheme.FollowedHyperlink;
            return colors;
        }

        public static WordTheme FromDocument(WordprocessingDocument document, WordStyles styles)
        {
            var read = Read(document);
            if (read != null)
                return read;

            var result = new WordTheme();
            var fonts = styles.Root?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle?.GetFirstChild<RunFonts>();
            var latin = fonts?.Ascii?.Value ?? fonts?.HighAnsi?.Value;
            var east = fonts?.EastAsia?.Value;
            if (latin != null || east != null)
            {
                result.MinorLatin = result.MajorLatin = latin ?? east;
                result.MinorEastAsian = result.MajorEastAsian = east ?? latin;
            }

            return result;
        }

        public static bool IsKorean(string font)
        {
            if (string.IsNullOrWhiteSpace(font))
                return false;

            return font.Any(c => c >= '가' && c <= '힣') || s_koreanFonts.Any(k => font.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        public string Font(ThemeFontValues value)
        {
            if (value == ThemeFontValues.MajorEastAsia)
                return MajorEastAsian;

            if (value == ThemeFontValues.MinorEastAsia)
                return MinorEastAsian;

            if (value == ThemeFontValues.MajorAscii || value == ThemeFontValues.MajorHighAnsi || value == ThemeFontValues.MajorBidi)
                return MajorLatin;

            return MinorLatin;
        }

        public string Color(ThemeColorValues value)
        {
            string key;
            if (value == ThemeColorValues.Dark1 || value == ThemeColorValues.Text1)
                key = "dark1";
            else if (value == ThemeColorValues.Light1 || value == ThemeColorValues.Background1)
                key = "light1";
            else if (value == ThemeColorValues.Dark2 || value == ThemeColorValues.Text2)
                key = "dark2";
            else if (value == ThemeColorValues.Light2 || value == ThemeColorValues.Background2)
                key = "light2";
            else if (value == ThemeColorValues.Hyperlink)
                key = "hyperlink";
            else if (value == ThemeColorValues.FollowedHyperlink)
                key = "followed_hyperlink";
            else if (value == ThemeColorValues.Accent1)
                key = "accent1";
            else if (value == ThemeColorValues.Accent2)
                key = "accent2";
            else if (value == ThemeColorValues.Accent3)
                key = "accent3";
            else if (value == ThemeColorValues.Accent4)
                key = "accent4";
            else if (value == ThemeColorValues.Accent5)
                key = "accent5";
            else if (value == ThemeColorValues.Accent6)
                key = "accent6";
            else
                return null;

            return Colors.TryGetValue(key, out var color) ? color : null;
        }

        public string Resolve(DocumentFormat.OpenXml.Wordprocessing.Color color)
        {
            if (color?.ThemeColor?.Value == null)
                return color?.Val?.Value;

            var baseColor = Color(color.ThemeColor.Value);
            if (baseColor == null)
                return color.Val?.Value;

            if (TryFactor(color.ThemeShade?.Value, out var shade))
                return Shade(baseColor, shade);

            if (TryFactor(color.ThemeTint?.Value, out var tint))
                return Tint(baseColor, tint);

            return baseColor;
        }

        public static string Shade(string rgb, double factor) => Luminance(rgb, l => l * factor, true);

        public static string Tint(string rgb, double factor) => Luminance(rgb, l => l * factor + (1 - factor), false);

        public static bool TryFactor(string hex, out double factor)
        {
            factor = 1;
            if (string.IsNullOrEmpty(hex) || !int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
                return false;

            factor = value / 255d;
            return true;
        }

        public static string FactorHex(double factor) => ((int)Math.Round(factor * 255)).ToString("X2", CultureInfo.InvariantCulture);

        public static void Write(ThemePart part, WordTheme theme)
        {
            var elements = part.Theme.ThemeElements;
            var scheme = elements.ColorScheme ?? (elements.ColorScheme = new A.ColorScheme());
            scheme.Name = theme.ColorSchemeName;
            Set(scheme, "dark1", theme.Colors, true);
            Set(scheme, "light1", theme.Colors, true);
            Set(scheme, "dark2", theme.Colors, false);
            Set(scheme, "light2", theme.Colors, false);
            for (var i = 1; i <= 6; i++)
                Set(scheme, "accent" + i.ToString(CultureInfo.InvariantCulture), theme.Colors, false);

            Set(scheme, "hyperlink", theme.Colors, false);
            Set(scheme, "followed_hyperlink", theme.Colors, false);

            var fonts = elements.FontScheme ?? (elements.FontScheme = new A.FontScheme());
            fonts.Name = theme.FontSchemeName;
            Write(fonts.MajorFont ??= new A.MajorFont(), theme.MajorLatin, theme.MajorEastAsian);
            Write(fonts.MinorFont ??= new A.MinorFont(), theme.MinorLatin, theme.MinorEastAsian);
        }

        private static void Write(A.FontCollectionType collection, string latin, string eastAsian)
        {
            var face = collection.LatinFont ?? (collection.LatinFont = new A.LatinFont());
            if (face.Typeface?.Value != latin)
            {
                face.Typeface = latin;
                face.Panose = null;
                face.PitchFamily = null;
                face.CharacterSet = null;
            }

            var east = collection.EastAsianFont ?? (collection.EastAsianFont = new A.EastAsianFont());
            if (east.Typeface?.Value != eastAsian)
            {
                east.Typeface = eastAsian;
                east.Panose = null;
                east.PitchFamily = null;
                east.CharacterSet = null;
            }

            collection.ComplexScriptFont ??= new A.ComplexScriptFont { Typeface = string.Empty };
            var hang = collection.Elements<A.SupplementalFont>().FirstOrDefault(f => f.Script?.Value == "Hang");
            if (hang == null)
            {
                hang = new A.SupplementalFont { Script = "Hang", Typeface = eastAsian };
                var last = collection.Elements<A.SupplementalFont>().LastOrDefault();
                if (last != null)
                    last.InsertAfterSelf(hang);
                else
                    collection.ComplexScriptFont.InsertAfterSelf(hang);
            }
            else
            {
                hang.Typeface = eastAsian;
            }
        }

        private static void Set(A.ColorScheme scheme, string key, Dictionary<string, string> colors, bool system)
        {
            if (!colors.TryGetValue(key, out var value))
                return;

            DocumentFormat.OpenXml.OpenXmlCompositeElement holder = key switch
            {
                "dark1" => scheme.Dark1Color ??= new A.Dark1Color(),
                "light1" => scheme.Light1Color ??= new A.Light1Color(),
                "dark2" => scheme.Dark2Color ??= new A.Dark2Color(),
                "light2" => scheme.Light2Color ??= new A.Light2Color(),
                "accent1" => scheme.Accent1Color ??= new A.Accent1Color(),
                "accent2" => scheme.Accent2Color ??= new A.Accent2Color(),
                "accent3" => scheme.Accent3Color ??= new A.Accent3Color(),
                "accent4" => scheme.Accent4Color ??= new A.Accent4Color(),
                "accent5" => scheme.Accent5Color ??= new A.Accent5Color(),
                "accent6" => scheme.Accent6Color ??= new A.Accent6Color(),
                "hyperlink" => scheme.Hyperlink ??= new A.Hyperlink(),
                _ => scheme.FollowedHyperlinkColor ??= new A.FollowedHyperlinkColor(),
            };

            var current = Value(holder);
            if (string.Equals(current, value, StringComparison.OrdinalIgnoreCase) && holder.HasChildren)
                return;

            holder.RemoveAllChildren();
            if (system && key == "dark1" && value == "000000")
                holder.AppendChild(new A.SystemColor { Val = A.SystemColorValues.WindowText, LastColor = "000000" });
            else if (system && key == "light1" && value == "FFFFFF")
                holder.AppendChild(new A.SystemColor { Val = A.SystemColorValues.Window, LastColor = "FFFFFF" });
            else
                holder.AppendChild(new A.RgbColorModelHex { Val = value });
        }

        private static void Put(Dictionary<string, string> colors, string key, DocumentFormat.OpenXml.OpenXmlCompositeElement holder)
        {
            var value = Value(holder);
            if (value != null)
                colors[key] = value;
        }

        private static string Value(DocumentFormat.OpenXml.OpenXmlCompositeElement holder)
        {
            if (holder == null)
                return null;

            var rgb = holder.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value;
            if (rgb != null)
                return rgb.ToUpperInvariant();

            var system = holder.GetFirstChild<A.SystemColor>();
            return system?.LastColor?.Value?.ToUpperInvariant();
        }

        private static string Face(A.TextFontType font)
        {
            var name = font?.Typeface?.Value;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private static string Script(A.FontCollectionType collection)
        {
            var name = collection?.Elements<A.SupplementalFont>().FirstOrDefault(f => f.Script?.Value == "Hang")?.Typeface?.Value;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private static int Byte(double value, bool floor) => (int)(floor ? Math.Floor(value * 255 + 1e-9) : Math.Round(value * 255));

        private static string Luminance(string rgb, Func<double, double> change, bool floor)
        {
            if (rgb == null || rgb.Length != 6)
                return rgb;

            var r = Convert.ToInt32(rgb.Substring(0, 2), 16) / 255d;
            var g = Convert.ToInt32(rgb.Substring(2, 2), 16) / 255d;
            var b = Convert.ToInt32(rgb.Substring(4, 2), 16) / 255d;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var l = (max + min) / 2;
            double h = 0;
            double s = 0;
            if (max != min)
            {
                var d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                if (max == r)
                    h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g)
                    h = (b - r) / d + 2;
                else
                    h = (r - g) / d + 4;

                h /= 6;
            }

            l = Math.Clamp(change(l), 0, 1);
            double Channel(double p, double q, double t)
            {
                if (t < 0)
                    t += 1;

                if (t > 1)
                    t -= 1;

                if (t < 1d / 6)
                    return p + (q - p) * 6 * t;

                if (t < 0.5)
                    return q;

                if (t < 2d / 3)
                    return p + (q - p) * (2d / 3 - t) * 6;

                return p;
            }

            double red;
            double green;
            double blue;
            if (s == 0)
            {
                red = green = blue = l;
            }
            else
            {
                var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                var p = 2 * l - q;
                red = Channel(p, q, h + 1d / 3);
                green = Channel(p, q, h);
                blue = Channel(p, q, h - 1d / 3);
            }

            return string.Concat(
                Byte(red, floor).ToString("X2", CultureInfo.InvariantCulture),
                Byte(green, floor).ToString("X2", CultureInfo.InvariantCulture),
                Byte(blue, floor).ToString("X2", CultureInfo.InvariantCulture));
        }
    }
}
