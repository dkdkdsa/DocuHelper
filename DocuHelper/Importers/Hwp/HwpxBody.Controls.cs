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
                    ReadHeaderFooter(element, false);
                    break;
                case "footer":
                    ReadHeaderFooter(element, true);
                    break;
                case "pageNum":
                    ReadPageNumber(element);
                    break;
                case "newNum":
                    ReadNewNumber(element);
                    break;
                case "pageHiding":
                    ReadPageHiding(element);
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
