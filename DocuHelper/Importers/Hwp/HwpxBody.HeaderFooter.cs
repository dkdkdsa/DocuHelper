using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxBody
    {
        private void ReadHeaderFooter(XElement element)
        {
            if (HwpxXml.HasContent(element))
                _document.Skipped.Add(HwpSkipped.HeaderFooter);
        }

        private void ReadPageNumber(XElement element)
        {
            if (HwpxXml.Attribute(element, "pos") != "NONE")
                _document.Skipped.Add(HwpSkipped.HeaderFooter);
        }
    }
}
