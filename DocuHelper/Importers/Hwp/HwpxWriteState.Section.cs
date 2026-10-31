using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        public XElement Section(Body body)
        {
            var paragraphs = new List<XElement>();
            Blocks(body.ChildElements, paragraphs);
            if (paragraphs.Count == 0)
                paragraphs.Add(Paragraph(0, 0, false, new List<XElement> { TextRun(0, string.Empty) }));

            var first = paragraphs[0];
            first.AddFirst(new XElement(Hp + "run", new XAttribute("charPrIDRef", "0"), SectionProperties(body), Columns()));
            return new XElement(Hs + "sec", Namespaces(), paragraphs);
        }

        private XElement SectionProperties(Body body)
        {
            var section = body.Elements<SectionProperties>().LastOrDefault();
            var size = section?.GetFirstChild<PageSize>();
            var margin = section?.GetFirstChild<PageMargin>();
            var width = size?.Width?.Value is uint w && w > 0 ? Units((int)w) : 59528;
            var height = size?.Height?.Value is uint h && h > 0 ? Units((int)h) : 84188;
            var landscape = width > height;
            var header = margin?.Header?.Value is uint hd ? Units((int)hd) : 4252;
            var footer = margin?.Footer?.Value is uint ft ? Units((int)ft) : 4252;
            var top = margin?.Top?.Value is int t ? Units(t) - header : 5668;
            var bottom = margin?.Bottom?.Value is int b ? Units(b) - footer : 4252;
            var left = margin?.Left?.Value is uint l ? Units((int)l) : 8504;
            var right = margin?.Right?.Value is uint rt ? Units((int)rt) : 8504;
            if (top <= 0)
                top = 5668;

            if (bottom <= 0)
                bottom = 4252;

            if (landscape)
                (width, height) = (height, width);

            return new XElement(Hp + "secPr",
                new XAttribute("id", ""),
                new XAttribute("textDirection", "HORIZONTAL"),
                new XAttribute("spaceColumns", "1134"),
                new XAttribute("tabStop", "8000"),
                new XAttribute("tabStopVal", "4000"),
                new XAttribute("tabStopUnit", "HWPUNIT"),
                new XAttribute("outlineShapeIDRef", "0"),
                new XAttribute("memoShapeIDRef", "0"),
                new XAttribute("textVerticalWidthHead", "0"),
                new XAttribute("masterPageCnt", "0"),
                new XElement(Hp + "grid", new XAttribute("lineGrid", "0"), new XAttribute("charGrid", "0"), new XAttribute("wonggojiFormat", "0")),
                new XElement(Hp + "startNum", new XAttribute("pageStartsOn", "BOTH"), new XAttribute("page", "0"), new XAttribute("pic", "0"), new XAttribute("tbl", "0"), new XAttribute("equation", "0")),
                new XElement(Hp + "visibility",
                    new XAttribute("hideFirstHeader", "0"), new XAttribute("hideFirstFooter", "0"), new XAttribute("hideFirstMasterPage", "0"),
                    new XAttribute("border", "SHOW_ALL"), new XAttribute("fill", "SHOW_ALL"), new XAttribute("hideFirstPageNum", "0"),
                    new XAttribute("hideFirstEmptyLine", "0"), new XAttribute("showLineNumber", "0")),
                new XElement(Hp + "lineNumberShape", new XAttribute("restartType", "0"), new XAttribute("countBy", "0"), new XAttribute("distance", "0"), new XAttribute("startNumber", "0")),
                new XElement(Hp + "pagePr",
                    new XAttribute("landscape", landscape ? "NARROWLY" : "WIDELY"),
                    new XAttribute("width", Number(width)),
                    new XAttribute("height", Number(height)),
                    new XAttribute("gutterType", "LEFT_ONLY"),
                    new XElement(Hp + "margin",
                        new XAttribute("header", Number(header)), new XAttribute("footer", Number(footer)), new XAttribute("gutter", "0"),
                        new XAttribute("left", Number(left)), new XAttribute("right", Number(right)),
                        new XAttribute("top", Number(top)), new XAttribute("bottom", Number(bottom)))),
                NoteProperties("footNotePr", "-1", "283", "EACH_COLUMN"),
                NoteProperties("endNotePr", "14692344", "0", "END_OF_DOCUMENT"),
                PageBorder("BOTH"),
                PageBorder("EVEN"),
                PageBorder("ODD"));
        }

        private static XElement NoteProperties(string name, string length, string between, string place) => new(Hp + name,
            new XElement(Hp + "autoNumFormat", new XAttribute("type", "DIGIT"), new XAttribute("userChar", ""), new XAttribute("prefixChar", ""), new XAttribute("suffixChar", ")"), new XAttribute("supscript", "0")),
            new XElement(Hp + "noteLine", new XAttribute("length", length), new XAttribute("type", "SOLID"), new XAttribute("width", "0.12 mm"), new XAttribute("color", "#000000")),
            new XElement(Hp + "noteSpacing", new XAttribute("betweenNotes", between), new XAttribute("belowLine", "567"), new XAttribute("aboveLine", "850")),
            new XElement(Hp + "numbering", new XAttribute("type", "CONTINUOUS"), new XAttribute("newNum", "1")),
            new XElement(Hp + "placement", new XAttribute("place", place), new XAttribute("beneathText", "0")));

        private static XElement PageBorder(string type) => new(Hp + "pageBorderFill",
            new XAttribute("type", type),
            new XAttribute("borderFillIDRef", Number(PlainBorder)),
            new XAttribute("textBorder", "PAPER"),
            new XAttribute("headerInside", "0"),
            new XAttribute("footerInside", "0"),
            new XAttribute("fillArea", "PAPER"),
            new XElement(Hp + "offset", new XAttribute("left", "1417"), new XAttribute("right", "1417"), new XAttribute("top", "1417"), new XAttribute("bottom", "1417")));

        private static XElement Columns() => new(Hp + "ctrl",
            new XElement(Hp + "colPr", new XAttribute("id", ""), new XAttribute("type", "NEWSPAPER"), new XAttribute("layout", "LEFT"),
                new XAttribute("colCount", "1"), new XAttribute("sameSz", "1"), new XAttribute("sameGap", "0")));
    }
}
