using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private int _footnoteNumber;
        private int _endnoteNumber;
        private int _noteInstance = 1000;

        private XElement NoteRunOf(OpenXmlElement reference, bool endnote)
        {
            var id = endnote ? ((EndnoteReference)reference).Id?.Value : ((FootnoteReference)reference).Id?.Value;
            var main = _document.MainDocumentPart;
            OpenXmlElement note = endnote
                ? main?.EndnotesPart?.Endnotes?.Elements<Endnote>().FirstOrDefault(n => n.Id?.Value == id && n.Type == null)
                : main?.FootnotesPart?.Footnotes?.Elements<Footnote>().FirstOrDefault(n => n.Id?.Value == id && n.Type == null);
            if (note == null)
                return null;

            var number = endnote ? ++_endnoteNumber : ++_footnoteNumber;
            var paragraphs = new List<XElement>();
            Blocks(note.ChildElements, paragraphs);
            if (paragraphs.Count == 0)
                paragraphs.Add(Paragraph(0, 0, false, new[] { TextRun(0, string.Empty) }));

            paragraphs[0].AddFirst(AutoNumberRun(number, endnote));
            var sublist = new XElement(Hp + "subList",
                new XAttribute("id", string.Empty), new XAttribute("textDirection", "HORIZONTAL"), new XAttribute("lineWrap", "BREAK"),
                new XAttribute("vertAlign", "TOP"), new XAttribute("linkListIDRef", "0"), new XAttribute("linkListNextIDRef", "0"),
                new XAttribute("textWidth", "0"), new XAttribute("textHeight", "0"), new XAttribute("hasTextRef", "0"), new XAttribute("hasNumRef", "0"),
                paragraphs);
            var instance = Number(++_noteInstance);
            var control = new XElement(Hp + (endnote ? "endNote" : "footNote"),
                new XAttribute("number", Number(number)), new XAttribute("suffixChar", ")"), new XAttribute("instId", instance),
                sublist);
            return new XElement(Hp + "run", new XAttribute("charPrIDRef", "0"), new XElement(Hp + "ctrl", control));
        }

        private static XElement AutoNumberRun(int number, bool endnote) => new(Hp + "run", new XAttribute("charPrIDRef", "0"),
            new XElement(Hp + "ctrl",
                new XElement(Hp + "autoNum", new XAttribute("num", Number(number)), new XAttribute("numType", endnote ? "ENDNOTE" : "FOOTNOTE"),
                    new XElement(Hp + "autoNumFormat", new XAttribute("type", "DIGIT"), new XAttribute("userChar", string.Empty),
                        new XAttribute("prefixChar", string.Empty), new XAttribute("suffixChar", ")"), new XAttribute("supscript", "0")))));
    }
}
