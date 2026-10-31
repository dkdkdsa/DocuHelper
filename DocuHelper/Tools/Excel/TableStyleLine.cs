namespace DocuHelper.Tools.Excel
{
    internal sealed class TableStyleLine
    {
        public TableStyleLine(string style, ThemeShade color)
        {
            Style = style;
            Color = color;
        }

        public string Style { get; }

        public ThemeShade Color { get; }
    }
}
