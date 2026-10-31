using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal static class ConditionalExtensions
    {
        public const string X14Namespace = "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main";

        public static string LinkId(ConditionalFormattingRule rule) =>
            rule.Descendants().FirstOrDefault(e => e.LocalName == "id" && e.NamespaceUri == X14Namespace)?.InnerText;

        public static bool HasLink(ConditionalFormattingRule rule) => LinkId(rule) != null;

        public static void DropLink(ConditionalFormattingRule rule)
        {
            foreach (var list in rule.ChildElements.Where(c => c.LocalName == "extLst").ToList())
            {
                foreach (var ext in list.ChildElements.Where(e => e.Descendants().Any(d => d.LocalName == "id" && d.NamespaceUri == X14Namespace)).ToList())
                    ext.Remove();

                if (!list.HasChildren)
                    list.Remove();
            }
        }

        public static List<OpenXmlElement> Rules(Worksheet worksheet) =>
            Blocks(worksheet).SelectMany(b => b.ChildElements.Where(c => c.LocalName == "cfRule")).ToList();

        public static List<OpenXmlElement> Blocks(Worksheet worksheet) =>
            worksheet.GetFirstChild<WorksheetExtensionList>()?.Descendants().Where(e => e.LocalName == "conditionalFormatting" && e.NamespaceUri == X14Namespace).ToList()
            ?? new List<OpenXmlElement>();

        public static string IdOf(OpenXmlElement rule) => Attribute(rule, "id");

        public static int PriorityOf(OpenXmlElement rule) =>
            int.TryParse(Attribute(rule, "priority"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

        public static void SetPriority(OpenXmlElement rule, int priority) =>
            rule.SetAttribute(new OpenXmlAttribute("priority", string.Empty, priority.ToString(CultureInfo.InvariantCulture)));

        public static void ShiftPriorities(Worksheet worksheet, int by)
        {
            foreach (var rule in Rules(worksheet))
                SetPriority(rule, PriorityOf(rule) + by);
        }

        public static string SqrefOf(OpenXmlElement block) =>
            block.ChildElements.FirstOrDefault(c => c.LocalName == "sqref" && c.NamespaceUri == WorkbookFormulas.ExcelMainNamespace)?.InnerText;

        public static void SetSqref(OpenXmlElement block, string sqref)
        {
            if (block.ChildElements.FirstOrDefault(c => c.LocalName == "sqref" && c.NamespaceUri == WorkbookFormulas.ExcelMainNamespace) is OpenXmlLeafTextElement leaf)
                leaf.Text = sqref;
        }

        public static bool HasFormula(OpenXmlElement block) =>
            block.Descendants().Any(d => d.LocalName == "f" && d.NamespaceUri == WorkbookFormulas.ExcelMainNamespace);

        public static OpenXmlElement BlockOfLink(Worksheet worksheet, string id) =>
            Blocks(worksheet).FirstOrDefault(b => b.ChildElements.Any(c => c.LocalName == "cfRule" && IdOf(c) == id));

        public static void RemoveLinked(Worksheet worksheet, ICollection<string> ids)
        {
            if (ids.Count == 0)
                return;

            foreach (var rule in Rules(worksheet).Where(r => ids.Contains(IdOf(r))).ToList())
                rule.Remove();

            Prune(worksheet);
        }

        public static void RemoveBlocks(Worksheet worksheet, IEnumerable<OpenXmlElement> blocks)
        {
            foreach (var block in blocks.ToList())
                block.Remove();

            Prune(worksheet);
        }

        public static void Prune(Worksheet worksheet)
        {
            var list = worksheet.GetFirstChild<WorksheetExtensionList>();
            if (list == null)
                return;

            foreach (var block in Blocks(worksheet).Where(b => !b.ChildElements.Any(c => c.LocalName == "cfRule")).ToList())
                block.Remove();

            foreach (var group in list.Descendants().Where(e => e.LocalName == "conditionalFormattings" && e.NamespaceUri == X14Namespace).ToList())
            {
                if (group.HasChildren)
                    continue;

                var ext = group.Parent;
                group.Remove();
                if (ext != null && !ext.HasChildren)
                    ext.Remove();
            }

            if (!list.HasChildren)
                list.Remove();
        }

        private static string Attribute(OpenXmlElement element, string name) =>
            element.GetAttributes().FirstOrDefault(a => a.LocalName == name && string.IsNullOrEmpty(a.NamespaceUri)).Value;
    }
}
