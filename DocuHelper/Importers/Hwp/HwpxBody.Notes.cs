using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxBody
    {
        private int _footnotes;
        private int _endnotes;
        private bool _dropNumber;
        private bool _droppedNumber;

        private void ReadNote(XElement element, bool endnote, HwpxCharShape shape, HwpxRunBuilder runs)
        {
            if (_deleting > 0)
            {
                _deleted++;
                return;
            }

            var mark = endnote
                ? "미주" + (++_endnotes).ToString(CultureInfo.InvariantCulture) + ")"
                : (++_footnotes).ToString(CultureInfo.InvariantCulture) + ")";
            runs.AppendMark(mark, shape);
            var note = new HwpNote { Mark = mark };
            _document.Notes.Add(note);

            var drop = _dropNumber;
            var dropped = _droppedNumber;
            _dropNumber = true;
            _droppedNumber = false;
            ReadList(HwpxXml.Child(element, "subList"), note.Blocks);
            if (_droppedNumber)
                TrimStart(note.Blocks);

            _dropNumber = drop;
            _droppedNumber = dropped;
        }

        private void ReadNumber(XElement element, HwpxCharShape shape, HwpxRunBuilder runs)
        {
            var type = HwpxXml.Attribute(element, "numType");
            if (_dropNumber && (type == "FOOTNOTE" || type == "ENDNOTE"))
            {
                _dropNumber = false;
                _droppedNumber = true;
                return;
            }

            if (type == "PAGE")
            {
                runs.AppendField("PAGE", "1", shape);
                return;
            }

            var format = HwpxXml.Child(element, "autoNumFormat");
            var number = HwpxNumberFormat.Format(HwpxXml.Int(element, "num", 1), HwpxXml.Attribute(format, "type") ?? "DIGIT");
            Emit(HwpxXml.Attribute(format, "prefixChar") + number + HwpxXml.Attribute(format, "suffixChar"), shape, runs);
        }

        private static void TrimStart(List<HwpBlock> blocks)
        {
            if (blocks.Count == 0 || blocks[0] is not HwpParagraph paragraph)
                return;

            while (paragraph.Runs.Count > 0)
            {
                var run = paragraph.Runs[0];
                run.Text = run.Text.TrimStart(' ');
                if (run.Text.Length > 0)
                    return;

                paragraph.Runs.RemoveAt(0);
            }
        }
    }
}
