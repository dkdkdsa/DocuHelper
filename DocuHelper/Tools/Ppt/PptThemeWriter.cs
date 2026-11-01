using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptThemeWriter
    {
        private const string HangScript = "Hang";

        public static void Colors(A.Theme theme, string name, IReadOnlyList<string> hexes)
        {
            var scheme = theme.ThemeElements?.ColorScheme
                ?? throw new ToolInputException("테마의 색 구성표를 읽지 못해서 색을 바꿀 수 없어요.");
            scheme.Name = name;
            for (var i = 0; i < PptThemeInfo.Slots.Count; i++)
            {
                var slot = PptThemeInfo.Slots[i];
                var replacement = Slot(slot, hexes[i]);
                var existing = scheme.ChildElements.FirstOrDefault(c => c.LocalName == slot);
                if (existing == null)
                {
                    scheme.AppendChild(replacement);
                    continue;
                }

                existing.InsertBeforeSelf(replacement);
                existing.Remove();
            }
        }

        public static void Fonts(A.Theme theme, string name, string heading, string body, bool latinOnly)
        {
            var scheme = theme.ThemeElements?.FontScheme
                ?? throw new ToolInputException("테마의 글꼴 구성표를 읽지 못해서 글꼴을 바꿀 수 없어요.");
            scheme.Name = name;
            if (heading != null)
                Typefaces(scheme.MajorFont, heading, latinOnly);

            if (body != null)
                Typefaces(scheme.MinorFont, body, latinOnly);
        }

        public static void Background(P.SlideMaster master, bool dark)
        {
            var map = master.ColorMap ?? throw new ToolInputException("마스터의 색 대응표를 읽지 못해서 배경 색을 바꿀 수 없어요.");
            map.Background1 = dark ? A.ColorSchemeIndexValues.Dark1 : A.ColorSchemeIndexValues.Light1;
            map.Text1 = dark ? A.ColorSchemeIndexValues.Light1 : A.ColorSchemeIndexValues.Dark1;
            map.Background2 = dark ? A.ColorSchemeIndexValues.Dark2 : A.ColorSchemeIndexValues.Light2;
            map.Text2 = dark ? A.ColorSchemeIndexValues.Light2 : A.ColorSchemeIndexValues.Dark2;
        }

        private static OpenXmlElement Slot(string slot, string hex)
        {
            var color = new A.RgbColorModelHex { Val = hex };
            return slot switch
            {
                "dk1" => new A.Dark1Color(color),
                "lt1" => new A.Light1Color(color),
                "dk2" => new A.Dark2Color(color),
                "lt2" => new A.Light2Color(color),
                "accent1" => new A.Accent1Color(color),
                "accent2" => new A.Accent2Color(color),
                "accent3" => new A.Accent3Color(color),
                "accent4" => new A.Accent4Color(color),
                "accent5" => new A.Accent5Color(color),
                "accent6" => new A.Accent6Color(color),
                "hlink" => new A.Hyperlink(color),
                _ => new A.FollowedHyperlinkColor(color),
            };
        }

        private static void Typefaces(OpenXmlElement font, string typeface, bool latinOnly)
        {
            var latin = font.GetFirstChild<A.LatinFont>();
            if (latin == null)
                font.AppendChild(new A.LatinFont { Typeface = typeface });
            else
                latin.Typeface = typeface;

            if (!latinOnly)
            {
                var east = font.GetFirstChild<A.EastAsianFont>();
                if (east == null)
                    font.AppendChild(new A.EastAsianFont { Typeface = typeface });
                else
                    east.Typeface = typeface;
            }

            var complex = font.GetFirstChild<A.ComplexScriptFont>();
            if (complex == null)
                font.AppendChild(new A.ComplexScriptFont { Typeface = string.Empty });
            else
                complex.Typeface = string.Empty;

            if (latinOnly)
                return;

            var hang = font.Elements<A.SupplementalFont>().FirstOrDefault(f => f.Script?.Value == HangScript);
            if (hang == null)
                font.AppendChild(new A.SupplementalFont { Script = HangScript, Typeface = typeface });
            else
                hang.Typeface = typeface;
        }
    }
}
