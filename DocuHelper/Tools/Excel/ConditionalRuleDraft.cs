using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed class ConditionalRuleDraft
    {
        public ConditionalRuleDraft(ConditionalFormattingRule rule, ExcelDxfLook look, string label, bool defaultLook, string ignored)
        {
            Rule = rule;
            Look = look;
            Label = label;
            DefaultLook = defaultLook;
            Ignored = ignored;
        }

        public ConditionalFormattingRule Rule { get; }

        public ExcelDxfLook Look { get; }

        public string Label { get; }

        public bool DefaultLook { get; }

        public string Ignored { get; }

        public bool IsRelative => ConditionalRuleBuilder.IsRelative(Rule);
    }
}
