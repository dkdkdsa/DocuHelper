namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelStyleColor
    {
        public const uint Dark2Theme = 3;
        public const uint Accent1Theme = 4;
        public const uint HyperlinkTheme = 10;

        private ExcelStyleColor(string hex, uint? theme, double tint)
        {
            Hex = hex;
            Theme = theme;
            Tint = tint;
        }

        public string Hex { get; }

        public uint? Theme { get; }

        public double Tint { get; }

        public bool IsTheme => Theme.HasValue;

        public static ExcelStyleColor FromHex(string hex) => new(hex, null, 0);

        public static ExcelStyleColor FromTheme(uint theme, double tint = 0) => new(null, theme, tint);
    }
}
