namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelBorder
    {
        public static readonly ExcelBorder None = new(StyleChange.NoBorder, null);

        public ExcelBorder(string style, string color)
        {
            Style = style;
            Color = color;
        }

        public string Style { get; }

        public string Color { get; }

        public bool IsNone => Style == StyleChange.NoBorder;

        public int Weight => Style switch
        {
            "hair" => 0,
            "thin" or "dotted" or "dashed" or "dashDot" or "dashDotDot" => 1,
            "medium" or "mediumDashed" or "mediumDashDot" or "mediumDashDotDot" or "slantDashDot" => 2,
            "double" => 3,
            "thick" => 4,
            _ => -1,
        };

        public override string ToString() => Color == null ? Style : Style + " " + Color;
    }
}
