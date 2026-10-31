using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public static class PptColor
    {
        public const string InvalidMessage = "색은 #RRGGBB(예: #1F4E79)나 테마 색(예: accent1, accent1+40)으로 알려 주세요.";

        private const string PresetTable =
            "aliceBlue=F0F8FF,antiqueWhite=FAEBD7,aqua=00FFFF,aquamarine=7FFFD4,azure=F0FFFF,beige=F5F5DC,bisque=FFE4C4,black=000000,"
            + "blanchedAlmond=FFEBCD,blue=0000FF,blueViolet=8A2BE2,brown=A52A2A,burlyWood=DEB887,cadetBlue=5F9EA0,chartreuse=7FFF00,"
            + "chocolate=D2691E,coral=FF7F50,cornflowerBlue=6495ED,cornsilk=FFF8DC,crimson=DC143C,cyan=00FFFF,darkBlue=00008B,"
            + "darkCyan=008B8B,darkGoldenrod=B8860B,darkGray=A9A9A9,darkGreen=006400,darkKhaki=BDB76B,darkMagenta=8B008B,"
            + "darkOliveGreen=556B2F,darkOrange=FF8C00,darkOrchid=9932CC,darkRed=8B0000,darkSalmon=E9967A,darkSeaGreen=8FBC8F,"
            + "darkSlateBlue=483D8B,darkSlateGray=2F4F4F,darkTurquoise=00CED1,darkViolet=9400D3,deepPink=FF1493,deepSkyBlue=00BFFF,"
            + "dimGray=696969,dodgerBlue=1E90FF,firebrick=B22222,floralWhite=FFFAF0,forestGreen=228B22,fuchsia=FF00FF,gainsboro=DCDCDC,"
            + "ghostWhite=F8F8FF,gold=FFD700,goldenrod=DAA520,gray=808080,green=008000,greenYellow=ADFF2F,honeydew=F0FFF0,hotPink=FF69B4,"
            + "indianRed=CD5C5C,indigo=4B0082,ivory=FFFFF0,khaki=F0E68C,lavender=E6E6FA,lavenderBlush=FFF0F5,lawnGreen=7CFC00,"
            + "lemonChiffon=FFFACD,lightBlue=ADD8E6,lightCoral=F08080,lightCyan=E0FFFF,lightGoldenrodYellow=FAFAD2,lightGray=D3D3D3,"
            + "lightGreen=90EE90,lightPink=FFB6C1,lightSalmon=FFA07A,lightSeaGreen=20B2AA,lightSkyBlue=87CEFA,lightSlateGray=778899,"
            + "lightSteelBlue=B0C4DE,lightYellow=FFFFE0,lime=00FF00,limeGreen=32CD32,linen=FAF0E6,magenta=FF00FF,maroon=800000,"
            + "mediumAquamarine=66CDAA,mediumBlue=0000CD,mediumOrchid=BA55D3,mediumPurple=9370DB,mediumSeaGreen=3CB371,"
            + "mediumSlateBlue=7B68EE,mediumSpringGreen=00FA9A,mediumTurquoise=48D1CC,mediumVioletRed=C71585,midnightBlue=191970,"
            + "mintCream=F5FFFA,mistyRose=FFE4E1,moccasin=FFE4B5,navajoWhite=FFDEAD,navy=000080,oldLace=FDF5E6,olive=808000,"
            + "oliveDrab=6B8E23,orange=FFA500,orangeRed=FF4500,orchid=DA70D6,paleGoldenrod=EEE8AA,paleGreen=98FB98,"
            + "paleTurquoise=AFEEEE,paleVioletRed=DB7093,papayaWhip=FFEFD5,peachPuff=FFDAB9,peru=CD853F,pink=FFC0CB,plum=DDA0DD,"
            + "powderBlue=B0E0E6,purple=800080,red=FF0000,rosyBrown=BC8F8F,royalBlue=4169E1,saddleBrown=8B4513,salmon=FA8072,"
            + "sandyBrown=F4A460,seaGreen=2E8B57,seaShell=FFF5EE,sienna=A0522D,silver=C0C0C0,skyBlue=87CEEB,slateBlue=6A5ACD,"
            + "slateGray=708090,snow=FFFAFA,springGreen=00FF7F,steelBlue=4682B4,tan=D2B48C,teal=008080,thistle=D8BFD8,tomato=FF6347,"
            + "turquoise=40E0D0,violet=EE82EE,wheat=F5DEB3,white=FFFFFF,whiteSmoke=F5F5F5,yellow=FFFF00,yellowGreen=9ACD32";

        private static readonly Regex HexPattern = new(
            @"^#?(?<hex>[0-9a-f]{6}|[0-9a-f]{3})(?:(?<sign>[+-])(?<number>\d+(?:\.\d+)?)%?)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex SchemePattern = new(
            @"^(?<name>[a-z]+[1-6]?)(?:(?<sign>[+-])(?<number>\d+(?:\.\d+)?)%?)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex Separators = new(@"[\s_]+", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, string> SchemeNames = new(StringComparer.Ordinal)
        {
            ["accent1"] = "accent1",
            ["accent2"] = "accent2",
            ["accent3"] = "accent3",
            ["accent4"] = "accent4",
            ["accent5"] = "accent5",
            ["accent6"] = "accent6",
            ["light1"] = "bg1",
            ["background1"] = "bg1",
            ["bg1"] = "bg1",
            ["dark1"] = "tx1",
            ["text1"] = "tx1",
            ["tx1"] = "tx1",
            ["light2"] = "bg2",
            ["background2"] = "bg2",
            ["bg2"] = "bg2",
            ["dark2"] = "tx2",
            ["text2"] = "tx2",
            ["tx2"] = "tx2",
            ["hyperlink"] = "hlink",
            ["hlink"] = "hlink",
            ["followedhyperlink"] = "folHlink",
            ["folhlink"] = "folHlink",
            ["lt1"] = "lt1",
            ["dk1"] = "dk1",
            ["lt2"] = "lt2",
            ["dk2"] = "dk2",
        };

        private static readonly IReadOnlyDictionary<string, string> StandardMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bg1"] = "lt1",
            ["tx1"] = "dk1",
            ["bg2"] = "lt2",
            ["tx2"] = "dk2",
        };

        private static readonly Dictionary<string, string> Presets = BuildPresets();

        public static IReadOnlyDictionary<string, string> DefaultColorMap => StandardMap;

        public static bool TryParse(string text, bool allowNone, out PptColorSpec spec)
        {
            spec = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var value = text.Trim();
            var lowered = value.ToLowerInvariant();
            if (lowered == "auto" || lowered == "automatic" || value == "자동")
            {
                spec = PptColorSpec.Auto;
                return true;
            }

            if (allowNone && (lowered == "none" || lowered == "transparent" || value == "없음" || value == "투명"))
            {
                spec = PptColorSpec.None;
                return true;
            }

            var compact = Separators.Replace(value, string.Empty);
            var hex = HexPattern.Match(compact);
            if (hex.Success)
            {
                if (!TryPercent(hex, out var hexPercent))
                    return false;

                var digits = hex.Groups["hex"].Value;
                if (digits.Length == 3)
                    digits = string.Concat(digits.Select(c => new string(c, 2)));

                spec = PptColorSpec.FromHex(digits, hexPercent);
                return true;
            }

            var named = SchemePattern.Match(compact);
            if (!named.Success)
                return false;

            var key = named.Groups["name"].Value.ToLowerInvariant();
            if (!SchemeNames.TryGetValue(key, out var scheme))
                return false;

            if (!TryPercent(named, out var percent))
                return false;

            spec = PptColorSpec.FromScheme(scheme, percent);
            return true;
        }

        public static PptColorSpec Parse(string text, bool allowNone = false)
        {
            if (!TryParse(text, allowNone, out var spec))
                throw new ToolInputException(InvalidMessage);

            return spec;
        }

        public static OpenXmlCompositeElement ToElement(PptColorSpec spec)
        {
            if (spec == null || !spec.HasColor)
                throw new InvalidOperationException("Only a color with a value can become an element.");

            OpenXmlCompositeElement color = spec.IsHex
                ? new A.RgbColorModelHex { Val = spec.Hex }
                : new A.SchemeColor { Val = SchemeValue(spec.Scheme) };

            if (spec.Percent > 0)
            {
                var lighter = Math.Min(100, spec.Percent);
                color.AppendChild(new A.LuminanceModulation { Val = PptUnits.PercentToThousandths(100 - lighter) });
                color.AppendChild(new A.LuminanceOffset { Val = PptUnits.PercentToThousandths(lighter) });
            }
            else if (spec.Percent < 0)
            {
                var darker = Math.Min(100, -spec.Percent);
                color.AppendChild(new A.LuminanceModulation { Val = PptUnits.PercentToThousandths(100 - darker) });
            }

            return color;
        }

        public static PptColorSpec Read(OpenXmlElement colorElement)
        {
            var color = FindColorElement(colorElement);
            if (color == null)
                return null;

            PptColorSpec baseSpec;
            switch (color.LocalName)
            {
                case "srgbClr":
                    var hex = Attribute(color, "val");
                    if (hex == null || hex.Length != 6)
                        return null;

                    baseSpec = PptColorSpec.FromHex(hex);
                    break;
                case "schemeClr":
                    var scheme = Attribute(color, "val");
                    if (string.IsNullOrEmpty(scheme))
                        return null;

                    baseSpec = PptColorSpec.FromScheme(scheme);
                    break;
                default:
                    return null;
            }

            double modulation = 100;
            double offset = 0;
            foreach (var child in color.ChildElements)
            {
                switch (child.LocalName)
                {
                    case "lumMod":
                        modulation = PptUnits.ThousandthsToPercent(IntAttribute(child, "val", 100000));
                        break;
                    case "lumOff":
                        offset = PptUnits.ThousandthsToPercent(IntAttribute(child, "val", 0));
                        break;
                    case "alpha":
                        break;
                    default:
                        return null;
                }
            }

            if (offset > 0 && Math.Abs(modulation + offset - 100) < 0.001)
                return Rebuild(baseSpec, offset);

            if (offset == 0 && modulation < 100)
                return Rebuild(baseSpec, -(100 - modulation));

            return offset == 0 && modulation == 100 ? baseSpec : null;
        }

        public static bool IsColorElement(OpenXmlElement element) =>
            element != null && element.LocalName is "srgbClr" or "schemeClr" or "sysClr" or "prstClr" or "hslClr" or "scrgbClr";

        public static OpenXmlElement FindColorElement(OpenXmlElement element)
        {
            if (element == null)
                return null;

            if (IsColorElement(element))
                return element;

            return element.ChildElements.FirstOrDefault(IsColorElement);
        }

        public static string Resolve(PptColorSpec spec, PptThemeInfo theme, OpenXmlElement colorMap = null)
        {
            if (spec == null || !spec.HasColor)
                return null;

            return Resolve(ToElement(spec), theme, MapOf(colorMap), null);
        }

        public static string Resolve(OpenXmlElement colorElement, PptThemeInfo theme, OpenXmlElement colorMap) =>
            Resolve(colorElement, theme, MapOf(colorMap), null);

        public static string Resolve(OpenXmlElement colorElement, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap, string placeholderColor)
        {
            var color = FindColorElement(colorElement);
            if (color == null)
                return null;

            var rgb = BaseColor(color, theme ?? PptTheme.Default(), colorMap ?? StandardMap, placeholderColor);
            foreach (var transform in color.ChildElements)
                rgb = Transform(rgb, transform);

            return Format(rgb);
        }

        public static int AlphaOf(OpenXmlElement colorElement)
        {
            var color = FindColorElement(colorElement);
            if (color == null)
                return 100;

            double alpha = 100;
            foreach (var child in color.ChildElements)
            {
                switch (child.LocalName)
                {
                    case "alpha":
                        alpha = IntAttribute(child, "val", 100000) / 1000.0;
                        break;
                    case "alphaMod":
                        alpha *= IntAttribute(child, "val", 100000) / 100000.0;
                        break;
                    case "alphaOff":
                        alpha += IntAttribute(child, "val", 0) / 1000.0;
                        break;
                }
            }

            return (int)Math.Round(Math.Clamp(alpha, 0, 100), MidpointRounding.AwayFromZero);
        }

        public static IReadOnlyDictionary<string, string> MapOf(OpenXmlElement colorMap)
        {
            if (colorMap == null)
                return StandardMap;

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var attribute in colorMap.GetAttributes())
            {
                if (string.IsNullOrEmpty(attribute.NamespaceUri) && !string.IsNullOrEmpty(attribute.Value))
                    map[attribute.LocalName] = attribute.Value;
            }

            foreach (var pair in StandardMap)
                map.TryAdd(pair.Key, pair.Value);

            return map;
        }

        public static string PresetHex(string name) =>
            name != null && Presets.TryGetValue(name, out var hex) ? hex : null;

        private static PptColorSpec Rebuild(PptColorSpec baseSpec, double percent) =>
            baseSpec.IsHex ? PptColorSpec.FromHex(baseSpec.Hex, percent) : PptColorSpec.FromScheme(baseSpec.Scheme, percent);

        private static bool TryPercent(Match match, out double percent)
        {
            percent = 0;
            if (!match.Groups["sign"].Success)
                return true;

            if (!double.TryParse(match.Groups["number"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || number > 100)
                return false;

            percent = match.Groups["sign"].Value == "-" ? -number : number;
            return true;
        }

        private static A.SchemeColorValues SchemeValue(string token) => token switch
        {
            "accent1" => A.SchemeColorValues.Accent1,
            "accent2" => A.SchemeColorValues.Accent2,
            "accent3" => A.SchemeColorValues.Accent3,
            "accent4" => A.SchemeColorValues.Accent4,
            "accent5" => A.SchemeColorValues.Accent5,
            "accent6" => A.SchemeColorValues.Accent6,
            "bg1" => A.SchemeColorValues.Background1,
            "tx1" => A.SchemeColorValues.Text1,
            "bg2" => A.SchemeColorValues.Background2,
            "tx2" => A.SchemeColorValues.Text2,
            "lt1" => A.SchemeColorValues.Light1,
            "dk1" => A.SchemeColorValues.Dark1,
            "lt2" => A.SchemeColorValues.Light2,
            "dk2" => A.SchemeColorValues.Dark2,
            "hlink" => A.SchemeColorValues.Hyperlink,
            "folHlink" => A.SchemeColorValues.FollowedHyperlink,
            _ => A.SchemeColorValues.PhColor,
        };

        private static Dictionary<string, string> BuildPresets()
        {
            var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in PresetTable.Split(','))
            {
                var parts = entry.Split('=');
                table[parts[0]] = parts[1];
                if (parts[0].StartsWith("dark", StringComparison.Ordinal))
                    table["dk" + parts[0].Substring(4)] = parts[1];
                else if (parts[0].StartsWith("light", StringComparison.Ordinal))
                    table["lt" + parts[0].Substring(5)] = parts[1];
                else if (parts[0].StartsWith("medium", StringComparison.Ordinal))
                    table["med" + parts[0].Substring(6)] = parts[1];
            }

            foreach (var key in table.Keys.Where(k => k.EndsWith("Gray", StringComparison.OrdinalIgnoreCase)).ToList())
                table[key.Substring(0, key.Length - 4) + "Grey"] = table[key];

            return table;
        }

        private static Rgb BaseColor(OpenXmlElement color, PptThemeInfo theme, IReadOnlyDictionary<string, string> colorMap, string placeholderColor)
        {
            switch (color.LocalName)
            {
                case "srgbClr":
                    return ParseHex(Attribute(color, "val"));
                case "schemeClr":
                    var token = Attribute(color, "val") ?? "tx1";
                    if (token == "phClr")
                        return ParseHex(placeholderColor ?? theme.ColorOf("dk1"));

                    var slot = colorMap.TryGetValue(token, out var mapped) ? mapped : token;
                    return ParseHex(theme.ColorOf(slot) ?? theme.ColorOf("dk1"));
                case "sysClr":
                    var last = Attribute(color, "lastClr");
                    if (!string.IsNullOrEmpty(last))
                        return ParseHex(last);

                    return ParseHex(Attribute(color, "val") == "window" ? "FFFFFF" : "000000");
                case "prstClr":
                    return ParseHex(PresetHex(Attribute(color, "val")) ?? "000000");
                case "hslClr":
                    return FromHsl(
                        IntAttribute(color, "hue", 0) / (double)PptUnits.AnglePerDegree,
                        IntAttribute(color, "sat", 0) / 100000.0,
                        IntAttribute(color, "lum", 0) / 100000.0);
                case "scrgbClr":
                    return new Rgb(
                        FromLinear(IntAttribute(color, "r", 0) / 100000.0),
                        FromLinear(IntAttribute(color, "g", 0) / 100000.0),
                        FromLinear(IntAttribute(color, "b", 0) / 100000.0));
                default:
                    return ParseHex("000000");
            }
        }

        private static Rgb Transform(Rgb rgb, OpenXmlElement transform)
        {
            var value = IntAttribute(transform, "val", 0);
            var fraction = value / 100000.0;
            switch (transform.LocalName)
            {
                case "lumMod":
                case "lumOff":
                case "lum":
                case "satMod":
                case "satOff":
                case "sat":
                case "hueMod":
                case "hueOff":
                case "hue":
                    var (h, s, l) = ToHsl(rgb);
                    switch (transform.LocalName)
                    {
                        case "lumMod":
                            l *= fraction;
                            break;
                        case "lumOff":
                            l += fraction;
                            break;
                        case "lum":
                            l = fraction;
                            break;
                        case "satMod":
                            s *= fraction;
                            break;
                        case "satOff":
                            s += fraction;
                            break;
                        case "sat":
                            s = fraction;
                            break;
                        case "hueMod":
                            h *= fraction;
                            break;
                        case "hueOff":
                            h += value / (double)PptUnits.AnglePerDegree;
                            break;
                        default:
                            h = value / (double)PptUnits.AnglePerDegree;
                            break;
                    }

                    return FromHsl(h, Math.Clamp(s, 0, 1), Math.Clamp(l, 0, 1));
                case "tint":
                    return new Rgb(
                        FromLinear(ToLinear(rgb.R) * fraction + (1 - fraction)),
                        FromLinear(ToLinear(rgb.G) * fraction + (1 - fraction)),
                        FromLinear(ToLinear(rgb.B) * fraction + (1 - fraction)));
                case "shade":
                    return new Rgb(
                        FromLinear(ToLinear(rgb.R) * fraction),
                        FromLinear(ToLinear(rgb.G) * fraction),
                        FromLinear(ToLinear(rgb.B) * fraction));
                case "comp":
                    var (ch, cs, cl) = ToHsl(rgb);
                    return FromHsl(ch + 180, cs, cl);
                case "inv":
                    return new Rgb(1 - rgb.R, 1 - rgb.G, 1 - rgb.B);
                case "gray":
                    var gray = 0.299 * rgb.R + 0.587 * rgb.G + 0.114 * rgb.B;
                    return new Rgb(gray, gray, gray);
                case "red":
                    return new Rgb(Math.Clamp(fraction, 0, 1), rgb.G, rgb.B);
                case "redMod":
                    return new Rgb(Math.Clamp(rgb.R * fraction, 0, 1), rgb.G, rgb.B);
                case "redOff":
                    return new Rgb(Math.Clamp(rgb.R + fraction, 0, 1), rgb.G, rgb.B);
                case "green":
                    return new Rgb(rgb.R, Math.Clamp(fraction, 0, 1), rgb.B);
                case "greenMod":
                    return new Rgb(rgb.R, Math.Clamp(rgb.G * fraction, 0, 1), rgb.B);
                case "greenOff":
                    return new Rgb(rgb.R, Math.Clamp(rgb.G + fraction, 0, 1), rgb.B);
                case "blue":
                    return new Rgb(rgb.R, rgb.G, Math.Clamp(fraction, 0, 1));
                case "blueMod":
                    return new Rgb(rgb.R, rgb.G, Math.Clamp(rgb.B * fraction, 0, 1));
                case "blueOff":
                    return new Rgb(rgb.R, rgb.G, Math.Clamp(rgb.B + fraction, 0, 1));
                default:
                    return rgb;
            }
        }

        private static Rgb ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 6
                || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
            {
                return new Rgb(0, 0, 0);
            }

            return new Rgb(((number >> 16) & 255) / 255.0, ((number >> 8) & 255) / 255.0, (number & 255) / 255.0);
        }

        private static string Format(Rgb rgb) =>
            string.Concat(Channel(rgb.R).ToString("X2", CultureInfo.InvariantCulture), Channel(rgb.G).ToString("X2", CultureInfo.InvariantCulture), Channel(rgb.B).ToString("X2", CultureInfo.InvariantCulture));

        private static int Channel(double value) => (int)Math.Round(Math.Clamp(value, 0, 1) * 255, MidpointRounding.AwayFromZero);

        private static double ToLinear(double value) =>
            value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);

        private static double FromLinear(double value)
        {
            var clamped = Math.Clamp(value, 0, 1);
            return clamped <= 0.0031308 ? clamped * 12.92 : 1.055 * Math.Pow(clamped, 1 / 2.4) - 0.055;
        }

        private static (double H, double S, double L) ToHsl(Rgb rgb)
        {
            var max = Math.Max(rgb.R, Math.Max(rgb.G, rgb.B));
            var min = Math.Min(rgb.R, Math.Min(rgb.G, rgb.B));
            var lightness = (max + min) / 2;
            var delta = max - min;
            if (delta < 1e-12)
                return (0, 0, lightness);

            var saturation = delta / (1 - Math.Abs(2 * lightness - 1));
            double hue;
            if (max == rgb.R)
                hue = 60 * (((rgb.G - rgb.B) / delta) % 6);
            else if (max == rgb.G)
                hue = 60 * ((rgb.B - rgb.R) / delta + 2);
            else
                hue = 60 * ((rgb.R - rgb.G) / delta + 4);

            return (hue < 0 ? hue + 360 : hue, Math.Clamp(saturation, 0, 1), lightness);
        }

        private static Rgb FromHsl(double hue, double saturation, double lightness)
        {
            var h = hue % 360;
            if (h < 0)
                h += 360;

            var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            var x = chroma * (1 - Math.Abs(h / 60 % 2 - 1));
            var m = lightness - chroma / 2;
            double r, g, b;
            if (h < 60)
                (r, g, b) = (chroma, x, 0);
            else if (h < 120)
                (r, g, b) = (x, chroma, 0);
            else if (h < 180)
                (r, g, b) = (0, chroma, x);
            else if (h < 240)
                (r, g, b) = (0, x, chroma);
            else if (h < 300)
                (r, g, b) = (x, 0, chroma);
            else
                (r, g, b) = (chroma, 0, x);

            return new Rgb(r + m, g + m, b + m);
        }

        private static string Attribute(OpenXmlElement element, string name)
        {
            foreach (var attribute in element.GetAttributes())
            {
                if (attribute.LocalName == name && string.IsNullOrEmpty(attribute.NamespaceUri))
                    return attribute.Value;
            }

            return null;
        }

        private static int IntAttribute(OpenXmlElement element, string name, int fallback) =>
            int.TryParse(Attribute(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : fallback;

        private readonly record struct Rgb(double R, double G, double B);
    }
}
