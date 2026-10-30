namespace DocuHelper.Tools.Excel
{
    public sealed class FormulaToken
    {
        public FormulaToken(FormulaTokenKind kind, int start, int end, string text)
        {
            Kind = kind;
            Start = start;
            End = end;
            Text = text;
            Value = text;
        }

        public FormulaTokenKind Kind { get; }

        public int Start { get; }

        public int End { get; }

        public string Text { get; }

        public string Value { get; set; }

        public double Number { get; set; }

        public FormulaReference Reference { get; set; }
    }
}
