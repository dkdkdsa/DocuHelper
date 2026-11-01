using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpxNames;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxWriteState
    {
        private readonly Dictionary<string, HwpxStyle> _paraStyles = new();

        private int StyleIdOf(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph, int paraId, int charId)
        {
            var wordId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (wordId == null)
                return 0;

            var style = _styles.Find(wordId);
            if (WordStyles.NameOf(style) is "normal" || wordId == "Normal")
                return 0;

            if (!_paraStyles.TryGetValue(wordId, out var entry))
            {
                var word = style?.StyleName?.Val?.Value ?? wordId;
                entry = new HwpxStyle(_paraStyles.Count + 1, HwpStyleNames.ToHangul(word) ?? word, word, paraId, charId);
                _paraStyles[wordId] = entry;
            }

            return entry.Id;
        }

        private XElement StylesElement()
        {
            var element = new XElement(Hh + "styles", new XAttribute("itemCnt", Number(1 + _paraStyles.Count)),
                StyleElement(0, "바탕글", "Normal", 0, 0));
            foreach (var style in _paraStyles.Values.OrderBy(s => s.Id))
                element.Add(StyleElement(style.Id, style.Name, style.EngName, style.ParaPrId, style.CharPrId));

            return element;
        }

        private static XElement StyleElement(int id, string name, string engName, int paraPrId, int charPrId) => new(Hh + "style",
            new XAttribute("id", Number(id)), new XAttribute("type", "PARA"), new XAttribute("name", name),
            new XAttribute("engName", engName), new XAttribute("paraPrIDRef", Number(paraPrId)), new XAttribute("charPrIDRef", Number(charPrId)),
            new XAttribute("nextStyleIDRef", "0"), new XAttribute("langID", "1042"), new XAttribute("lockForm", "0"));
    }
}
