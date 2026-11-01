using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxBody
    {
        private static readonly Regex s_linkFlags = new(@";\d+;\d+;\d+;?$", RegexOptions.CultureInvariant);

        private static readonly Regex s_linkEscape = new(@"\\(.)", RegexOptions.CultureInvariant);

        private static void ReadFieldBegin(XElement element, HwpxRunBuilder runs)
        {
            var id = HwpxXml.Attribute(element, "id");
            var parameters = HwpxXml.Child(element, "parameters");
            switch (HwpxXml.Attribute(element, "type"))
            {
                case "HYPERLINK":
                    var target = LinkTarget(Parameter(parameters, "Command"));
                    if (!string.IsNullOrEmpty(target))
                        runs.OpenLink(id, target);

                    break;
                case "CLICK_HERE":
                    runs.OpenClickHere(id, new HwpClickHere { Name = HwpxXml.Attribute(element, "name"), Guide = Parameter(parameters, "Direction") });
                    break;
            }
        }

        private static string LinkTarget(string command)
        {
            if (string.IsNullOrEmpty(command))
                return null;

            return s_linkEscape.Replace(s_linkFlags.Replace(command, string.Empty), "$1");
        }

        private static string Parameter(XElement parameters, string name) =>
            parameters?.Elements().FirstOrDefault(e => HwpxXml.Attribute(e, "name") == name)?.Value;

        private void ReadControl(XElement element, HwpxCharShape shape, HwpxRunBuilder runs)
        {
            switch (element.Name.LocalName)
            {
                case "fieldBegin":
                    ReadFieldBegin(element, runs);
                    break;
                case "fieldEnd":
                    runs.CloseField(HwpxXml.Attribute(element, "beginIDRef"));
                    break;
                case "bookmark":
                    runs.AppendBookmark(HwpxXml.Attribute(element, "name"));
                    break;
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
