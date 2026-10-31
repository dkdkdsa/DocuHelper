namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelStyleLine
    {
        public ExcelStyleLine(string style, ExcelStyleColor color)
        {
            Style = style;
            Color = color;
        }

        public string Style { get; }

        public ExcelStyleColor Color { get; }
    }
}
