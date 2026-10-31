using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptTheme
    {
        private static readonly string[] DefaultColors =
        {
            "000000", "FFFFFF", "44546A", "E7E6E6", "4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47", "0563C1", "954F72",
        };

        public static PptThemeInfo Default() => new()
        {
            Name = "Office 테마",
            ColorSchemeName = "Office",
            FontSchemeName = "Office",
            Colors = new List<string>(DefaultColors),
            MajorLatin = "맑은 고딕",
            MajorEa = "맑은 고딕",
            MajorCs = string.Empty,
            MajorHang = string.Empty,
            MinorLatin = "맑은 고딕",
            MinorEa = "맑은 고딕",
            MinorCs = string.Empty,
            MinorHang = string.Empty,
        };

        public static PptThemeInfo Read(PresentationDocument document, SlideMasterPart masterPart = null)
        {
            var master = masterPart ?? document?.PresentationPart?.SlideMasterParts.FirstOrDefault();
            return Read(master?.ThemePart?.Theme);
        }

        public static PptThemeInfo Read(A.Theme theme)
        {
            var info = Default();
            if (theme == null)
                return info;

            info.Name = theme.Name?.Value ?? info.Name;
            var elements = theme.ThemeElements;
            var scheme = elements?.ColorScheme;
            if (scheme != null)
            {
                info.ColorSchemeName = scheme.Name?.Value ?? info.ColorSchemeName;
                for (var i = 0; i < PptThemeInfo.Slots.Count; i++)
                {
                    var slot = scheme.ChildElements.FirstOrDefault(c => c.LocalName == PptThemeInfo.Slots[i]);
                    var hex = PptColor.Resolve(slot, info, (OpenXmlElement)null);
                    if (hex != null)
                        info.Colors[i] = hex;
                }
            }

            var fonts = elements?.FontScheme;
            if (fonts != null)
            {
                info.FontSchemeName = fonts.Name?.Value ?? info.FontSchemeName;
                ReadFonts(fonts.ChildElements.FirstOrDefault(c => c.LocalName == "majorFont"), out var majorLatin, out var majorEa, out var majorCs, out var majorHang);
                ReadFonts(fonts.ChildElements.FirstOrDefault(c => c.LocalName == "minorFont"), out var minorLatin, out var minorEa, out var minorCs, out var minorHang);
                info.MajorLatin = majorLatin ?? info.MajorLatin;
                info.MajorEa = majorEa ?? info.MajorEa;
                info.MajorCs = majorCs ?? info.MajorCs;
                info.MajorHang = majorHang ?? info.MajorHang;
                info.MinorLatin = minorLatin ?? info.MinorLatin;
                info.MinorEa = minorEa ?? info.MinorEa;
                info.MinorCs = minorCs ?? info.MinorCs;
                info.MinorHang = minorHang ?? info.MinorHang;
            }

            return info;
        }

        public static string ResolveFont(string typeface, PptThemeInfo theme)
        {
            if (string.IsNullOrEmpty(typeface))
                return null;

            if (typeface[0] != '+')
                return typeface;

            var info = theme ?? Default();
            return typeface switch
            {
                "+mj-lt" => info.MajorLatin,
                "+mn-lt" => info.MinorLatin,
                "+mj-ea" => First(info.MajorEa, info.MajorHang, info.MajorLatin),
                "+mn-ea" => First(info.MinorEa, info.MinorHang, info.MinorLatin),
                "+mj-cs" => First(info.MajorCs, info.MajorLatin),
                "+mn-cs" => First(info.MinorCs, info.MinorLatin),
                _ => typeface,
            };
        }

        public static IReadOnlyDictionary<string, string> ColorMapOf(SlidePart slidePart)
        {
            var layoutPart = slidePart?.SlideLayoutPart;
            var map = new Dictionary<string, string>(PptColor.MapOf(layoutPart?.SlideMasterPart?.SlideMaster?.ColorMap), StringComparer.Ordinal);
            Override(map, layoutPart?.SlideLayout?.ColorMapOverride);
            Override(map, slidePart?.Slide?.ColorMapOverride);
            return map;
        }

        private static void Override(Dictionary<string, string> map, P.ColorMapOverride overrides)
        {
            var replacement = overrides?.ChildElements.FirstOrDefault(c => c.LocalName == "overrideClrMapping");
            if (replacement == null)
                return;

            foreach (var pair in PptColor.MapOf(replacement))
                map[pair.Key] = pair.Value;
        }

        private static string First(params string[] values) => values.FirstOrDefault(v => !string.IsNullOrEmpty(v));

        private static void ReadFonts(OpenXmlElement font, out string latin, out string ea, out string cs, out string hang)
        {
            latin = null;
            ea = null;
            cs = null;
            hang = null;
            if (font == null)
                return;

            foreach (var child in font.ChildElements)
            {
                switch (child.LocalName)
                {
                    case "latin":
                        latin = Typeface(child);
                        break;
                    case "ea":
                        ea = Typeface(child);
                        break;
                    case "cs":
                        cs = Typeface(child);
                        break;
                    case "font":
                        if (Attribute(child, "script") == "Hang")
                            hang = Typeface(child);

                        break;
                }
            }
        }

        private static string Typeface(OpenXmlElement element) => Attribute(element, "typeface") ?? string.Empty;

        private static string Attribute(OpenXmlElement element, string name)
        {
            foreach (var attribute in element.GetAttributes())
            {
                if (attribute.LocalName == name && string.IsNullOrEmpty(attribute.NamespaceUri))
                    return attribute.Value;
            }

            return null;
        }
    }
}
