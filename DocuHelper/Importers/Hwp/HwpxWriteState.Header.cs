using System.Linq;
using System.Xml.Linq;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private static readonly string[] s_languages = { "HANGUL", "LATIN", "HANJA", "JAPANESE", "OTHER", "SYMBOL", "USER" };

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

            var borders = new XElement(Hh + "borderFills", new XAttribute("itemCnt", Number(2 + _fills.Count)),
                BorderFill(PlainBorder, false, null),
                BorderFill(CellBorder, true, null));
            foreach (var pair in _fills.OrderBy(p => p.Value))
                borders.Add(BorderFill(pair.Value, true, pair.Key));

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
                    new XElement(Hh + "tabProperties", new XAttribute("itemCnt", "1"),
                        new XElement(Hh + "tabPr", new XAttribute("id", "0"), new XAttribute("autoTabLeft", "0"), new XAttribute("autoTabRight", "0"))),
                    paras,
                    new XElement(Hh + "styles", new XAttribute("itemCnt", "1"),
                        new XElement(Hh + "style",
                            new XAttribute("id", "0"), new XAttribute("type", "PARA"), new XAttribute("name", "바탕글"),
                            new XAttribute("engName", "Normal"), new XAttribute("paraPrIDRef", "0"), new XAttribute("charPrIDRef", "0"),
                            new XAttribute("nextStyleIDRef", "0"), new XAttribute("langID", "1042"), new XAttribute("lockForm", "0")))),
                new XElement(Hh + "compatibleDocument", new XAttribute("targetProgram", "HWP201X"), new XElement(Hh + "layoutCompatibility")),
                new XElement(Hh + "docOption", new XElement(Hh + "linkinfo", new XAttribute("path", ""), new XAttribute("pageInherit", "0"), new XAttribute("footnoteInherit", "0"))),
                new XElement(Hh + "trackchageConfig", new XAttribute("flags", "56")));
        }
    }
}
