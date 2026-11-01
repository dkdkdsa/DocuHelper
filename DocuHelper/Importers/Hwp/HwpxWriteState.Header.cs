using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private static readonly string[] s_languages = { "HANGUL", "LATIN", "HANJA", "JAPANESE", "OTHER", "SYMBOL", "USER" };

        private static XElement PageBorderFill(int id, HwpBorderLine[] lines, string fill)
        {
            XElement Side(string name, HwpBorderLine line) => new(Hh + name,
                new XAttribute("type", line == null ? "NONE" : line.Type),
                new XAttribute("width", line == null ? "0.1 mm" : line.WidthMm.ToString("0.00", CultureInfo.InvariantCulture) + " mm"),
                new XAttribute("color", line == null ? "#000000" : line.Color));

            var element = new XElement(Hh + "borderFill",
                new XAttribute("id", Number(id)),
                new XAttribute("threeD", "0"),
                new XAttribute("shadow", "0"),
                new XAttribute("centerLine", "NONE"),
                new XAttribute("breakCellSeparateLine", "0"),
                new XElement(Hh + "slash", new XAttribute("type", "NONE"), new XAttribute("Crooked", "0"), new XAttribute("isCounter", "0")),
                new XElement(Hh + "backSlash", new XAttribute("type", "NONE"), new XAttribute("Crooked", "0"), new XAttribute("isCounter", "0")),
                Side("leftBorder", lines[1]),
                Side("rightBorder", lines[3]),
                Side("topBorder", lines[0]),
                Side("bottomBorder", lines[2]),
                new XElement(Hh + "diagonal", new XAttribute("type", "SOLID"), new XAttribute("width", "0.1 mm"), new XAttribute("color", "#000000")));
            if (fill != null)
            {
                element.Add(new XElement(Hc + "fillBrush",
                    new XElement(Hc + "winBrush", new XAttribute("faceColor", "#" + fill), new XAttribute("hatchColor", "#999999"), new XAttribute("alpha", "0"))));
            }

            return element;
        }

        private XElement TabProperties()
        {
            var element = new XElement(Hh + "tabProperties", new XAttribute("itemCnt", Number(1 + _tabs.Count)),
                new XElement(Hh + "tabPr", new XAttribute("id", "0"), new XAttribute("autoTabLeft", "0"), new XAttribute("autoTabRight", "0")));
            foreach (var pair in _tabs.Values.OrderBy(t => t.Id))
            {
                element.Add(new XElement(Hh + "tabPr", new XAttribute("id", Number(pair.Id)), new XAttribute("autoTabLeft", "0"), new XAttribute("autoTabRight", "0"),
                    pair.Stops.Select(stop => new XElement(Hh + "tabItem", new XAttribute("pos", Number(stop.Position)), new XAttribute("type", stop.Type), new XAttribute("leader", stop.Leader), new XAttribute("unit", "HWPUNIT")))));
            }

            return element;
        }

        public XElement Header()
        {
            var fontfaces = new XElement(Hh + "fontfaces", new XAttribute("itemCnt", Number(s_languages.Length)));
            foreach (var language in s_languages)
            {
                var face = new XElement(Hh + "fontface", new XAttribute("lang", language), new XAttribute("fontCnt", Number(_fonts.Count)));
                for (var i = 0; i < _fonts.Count; i++)
                    face.Add(new XElement(Hh + "font", new XAttribute("id", Number(i)), new XAttribute("face", _fonts[i]), new XAttribute("type", "TTF"), new XAttribute("isEmbedded", "0")));

                fontfaces.Add(face);
            }

            var borders = new XElement(Hh + "borderFills", new XAttribute("itemCnt", Number(2 + _borderSpecs.Count)),
                BorderFill(PlainBorder, false, null),
                BorderFill(CellBorder, true, null));
            foreach (var spec in _borderSpecs)
                borders.Add(PageBorderFill(spec.Id, spec.Lines, spec.Fill));

            var chars = new XElement(Hh + "charProperties", new XAttribute("itemCnt", Number(_chars.Count)));
            foreach (var pair in _chars.OrderBy(p => p.Value))
                chars.Add(CharProperty(pair.Value, pair.Key));

            var paras = new XElement(Hh + "paraProperties", new XAttribute("itemCnt", Number(_paras.Count)));
            foreach (var pair in _paras.OrderBy(p => p.Value))
                paras.Add(ParaProperty(pair.Value, pair.Key));

            return new XElement(Hh + "head", Namespaces(),
                new XAttribute("version", "1.4"),
                new XAttribute("secCnt", "1"),
                new XElement(Hh + "beginNum",
                    new XAttribute("page", "1"), new XAttribute("footnote", "1"), new XAttribute("endnote", "1"),
                    new XAttribute("pic", "1"), new XAttribute("tbl", "1"), new XAttribute("equation", "1")),
                new XElement(Hh + "refList",
                    fontfaces,
                    borders,
                    chars,
                    TabProperties(),
                    NumberingsElement(),
                    BulletsElement(),
                    paras,
                    StylesElement()),
                new XElement(Hh + "compatibleDocument", new XAttribute("targetProgram", "HWP201X"), new XElement(Hh + "layoutCompatibility")),
                new XElement(Hh + "docOption", new XElement(Hh + "linkinfo", new XAttribute("path", ""), new XAttribute("pageInherit", "0"), new XAttribute("footnoteInherit", "0"))),
                new XElement(Hh + "trackchageConfig", new XAttribute("flags", "56")));
        }
    }
}
