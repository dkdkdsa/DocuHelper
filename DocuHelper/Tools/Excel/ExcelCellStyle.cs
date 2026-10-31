namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelCellStyle
    {
        public static readonly ExcelCellStyle Default = new() { FormatCode = "General" };

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public string FontColor { get; set; }

        public string FontName { get; set; }

        public double? FontSize { get; set; }

        public string FillColor { get; set; }

        public string Horizontal { get; set; }

        public string Vertical { get; set; }

        public bool Wrap { get; set; }

        public ExcelBorder Top { get; set; }

        public ExcelBorder Bottom { get; set; }

        public ExcelBorder Left { get; set; }

        public ExcelBorder Right { get; set; }

        public uint NumberFormatId { get; set; }

        public string FormatCode { get; set; }

        public bool Locked { get; set; } = true;

        public string StyleName { get; set; }

        public bool IsGeneral => ExcelNumberFormat.IsGeneral(FormatCode);

        public bool IsDate => ExcelNumberFormat.IsDate(FormatCode);

        public bool IsPercent => ExcelNumberFormat.IsPercent(FormatCode);

        public bool IsThousands => ExcelNumberFormat.IsThousands(FormatCode);

        public bool IsText => ExcelNumberFormat.IsText(FormatCode);
    }
}
