using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Excel
{
    internal sealed class ExcelColors
    {
        private static readonly string[] s_indexed =
        {
            "000000", "FFFFFF", "FF0000", "00FF00", "0000FF", "FFFF00", "FF00FF", "00FFFF",
            "000000", "FFFFFF", "FF0000", "00FF00", "0000FF", "FFFF00", "FF00FF", "00FFFF",
            "800000", "008000", "000080", "808000", "800080", "008080", "C0C0C0", "808080",
            "9999FF", "993366", "FFFFCC", "CCFFFF", "660066", "FF8080", "0066CC", "CCCCFF",
            "000080", "FF00FF", "FFFF00", "00FFFF", "800080", "800000", "008080", "0000FF",
            "00CCFF", "CCFFFF", "CCFFCC", "FFFF99", "99CCFF", "FF99CC", "CC99FF", "FFCC99",
            "3366FF", "33CCCC", "99CC00", "FFCC00", "FF9900", "FF6600", "666699", "969696",
            "003366", "339966", "003300", "333300", "993300", "993366", "333399", "333333",
        };

        private static readonly string[] s_defaultTheme =
        {
            "FFFFFF", "000000", "E7E6E6", "44546A", "4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47", "0563C1", "954F72",
        };

        private readonly WorkbookPart _workbookPart;
        private string[] _theme;
        private string[] _palette;

        public ExcelColors(WorkbookPart workbookPart)
        {
            _workbookPart = workbookPart;
        }

        public string Resolve(ColorType color)
        {
            if (color == null || color.Auto?.Value == true)
                return null;

            string hex = null;
            if (color.Rgb?.Value is string rgb && rgb.Length >= 6)
                hex = rgb.Substring(rgb.Length - 6);
            else if (color.Theme?.Value is uint theme)
                hex = Theme().ElementAtOrDefault((int)theme);
            else if (color.Indexed?.Value is uint indexed)
                hex = Palette().ElementAtOrDefault((int)indexed);

            if (hex == null || !IsHex(hex))
                return null;

            hex = hex.ToUpperInvariant();
            var tint = color.Tint?.Value ?? 0;
            return tint == 0 ? hex : Tint(hex, tint);
        }

        public static bool IsHex(string text) =>
            text != null && text.Length == 6 && text.All(Uri.IsHexDigit);

        private string[] Palette()
        {
            if (_palette != null)
                return _palette;

            var custom = _workbookPart?.WorkbookStylesPart?.Stylesheet?.Colors?.IndexedColors?.Elements<RgbColor>()
                .Select(c => c.Rgb?.Value is string rgb && rgb.Length >= 6 ? rgb.Substring(rgb.Length - 6) : null)
                .ToArray();
            _palette = custom != null && custom.Length > 0 ? custom : s_indexed;
            return _palette;
        }

        private string[] Theme()
        {
            if (_theme != null)
                return _theme;

            var scheme = _workbookPart?.ThemePart?.Theme?.ThemeElements?.ColorScheme;
            if (scheme == null)
                return _theme = s_defaultTheme;

            var colors = new List<string>
            {
                SchemeHex(scheme.Light1Color, 0),
                SchemeHex(scheme.Dark1Color, 1),
                SchemeHex(scheme.Light2Color, 2),
                SchemeHex(scheme.Dark2Color, 3),
                SchemeHex(scheme.Accent1Color, 4),
                SchemeHex(scheme.Accent2Color, 5),
                SchemeHex(scheme.Accent3Color, 6),
                SchemeHex(scheme.Accent4Color, 7),
                SchemeHex(scheme.Accent5Color, 8),
                SchemeHex(scheme.Accent6Color, 9),
                SchemeHex(scheme.Hyperlink, 10),
                SchemeHex(scheme.FollowedHyperlinkColor, 11),
            };
            return _theme = colors.ToArray();
        }

        private static string SchemeHex(A.Color2Type color, int index)
        {
            var hex = color?.RgbColorModelHex?.Val?.Value ?? color?.SystemColor?.LastColor?.Value;
            return IsHex(hex) ? hex : s_defaultTheme[index];
        }

        private static string Tint(string hex, double tint)
        {
            var r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber) / 255.0;
            var g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255.0;
            var b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255.0;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var light = (max + min) / 2;
            double hue = 0;
            double saturation = 0;
            if (max != min)
            {
                var delta = max - min;
                saturation = light > 0.5 ? delta / (2 - max - min) : delta / (max + min);
                if (max == r)
                    hue = ((g - b) / delta) + (g < b ? 6 : 0);
                else if (max == g)
                    hue = ((b - r) / delta) + 2;
                else
                    hue = ((r - g) / delta) + 4;

                hue /= 6;
            }

            light = tint < 0 ? light * (1 + tint) : (light * (1 - tint)) + tint;
            light = Math.Clamp(light, 0, 1);

            double red, green, blue;
            if (saturation == 0)
            {
                red = green = blue = light;
            }
            else
            {
                var q = light < 0.5 ? light * (1 + saturation) : light + saturation - (light * saturation);
                var p = (2 * light) - q;
                red = Channel(p, q, hue + (1.0 / 3));
                green = Channel(p, q, hue);
                blue = Channel(p, q, hue - (1.0 / 3));
            }

            return string.Concat(new[] { red, green, blue }.Select(v => ((int)Math.Round(v * 255)).ToString("X2", CultureInfo.InvariantCulture)));
        }

        private static double Channel(double p, double q, double t)
        {
            if (t < 0)
                t += 1;

            if (t > 1)
                t -= 1;

            if (t < 1.0 / 6)
                return p + ((q - p) * 6 * t);

            if (t < 1.0 / 2)
                return q;

            if (t < 2.0 / 3)
                return p + ((q - p) * ((2.0 / 3) - t) * 6);

            return p;
        }
    }
}
