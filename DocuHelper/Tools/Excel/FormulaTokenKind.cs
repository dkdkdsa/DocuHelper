namespace DocuHelper.Tools.Excel
{
    public enum FormulaTokenKind
    {
        Number,
        Text,
        Boolean,
        Error,
        Reference,
        Name,
        Function,
        Operator,
        OpenParen,
        CloseParen,
        Comma,
        Unknown,
    }
}
