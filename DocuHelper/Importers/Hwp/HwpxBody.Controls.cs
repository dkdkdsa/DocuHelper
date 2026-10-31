using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxBody
    {
        private void ReadControl(XElement element, HwpxCharShape shape, HwpxRunBuilder runs)
        {
            switch (element.Name.LocalName)
            {
                case "header":
                case "footer":
                    ReadHeaderFooter(element);
                    break;
                case "pageNum":
                    ReadPageNumber(element);
                    break;
                case "footNote":
                    ReadNote(element, false, shape, runs);
                    break;
                case "endNote":
                    ReadNote(element, true, shape, runs);
                    break;
                case "autoNum":
                    ReadNumber(element, shape, runs);
                    break;
            }
        }
    }
}
