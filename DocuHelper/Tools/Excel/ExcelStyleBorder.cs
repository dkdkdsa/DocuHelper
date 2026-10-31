namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelStyleBorder
    {
        public ExcelStyleBorder(ExcelStyleLine top, ExcelStyleLine bottom, ExcelStyleLine left, ExcelStyleLine right)
        {
            Top = top;
            Bottom = bottom;
            Left = left;
            Right = right;
        }

        public ExcelStyleLine Top { get; }

        public ExcelStyleLine Bottom { get; }

        public ExcelStyleLine Left { get; }

        public ExcelStyleLine Right { get; }

        public static ExcelStyleBorder Box(ExcelStyleLine line) => new(line, line, line, line);

        public static ExcelStyleBorder Under(ExcelStyleLine line) => new(null, line, null, null);

        public static ExcelStyleBorder OverUnder(ExcelStyleLine over, ExcelStyleLine under) => new(over, under, null, null);
    }
}
