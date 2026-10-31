using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpWordWriter;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpWordNotes
    {
        internal static IEnumerable<OpenXmlElement> NoteElements(HwpNote note, int width)
        {
            var elements = ToElements(note.Blocks, width);
            var mark = new Run(new Text((note.Mark ?? string.Empty) + " ") { Space = SpaceProcessingModeValues.Preserve });
            if (elements.FirstOrDefault() is Paragraph first)
            {
                if (first.ParagraphProperties != null)
                    first.ParagraphProperties.InsertAfterSelf(mark);
                else
                    first.PrependChild(mark);
            }
            else
            {
                elements.Insert(0, new Paragraph(mark));
            }

            return elements;
        }
    }
}
