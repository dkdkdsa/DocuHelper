namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelCellStyle
    {
        public static readonly ExcelCellStyle Default = new() { FormatCode = "General" };

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public string FontColor { get; set; }

        public string FillColor { get; set; }

        public string Horizontal { get; set; }

        public uint NumberFormatId { get; set; }

        public string FormatCode { get; set; }

        public bool IsGeneral => ExcelNumberFormat.IsGeneral(FormatCode);

        public bool IsDate => ExcelNumberFormat.IsDate(FormatCode);

        public bool IsPercent => ExcelNumberFormat.IsPercent(FormatCode);

        public bool IsThousands => ExcelNumberFormat.IsThousands(FormatCode);

        public bool IsText => ExcelNumberFormat.IsText(FormatCode);
    }
}
