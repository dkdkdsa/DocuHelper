namespace DocuHelper.Tools.Excel
{
    internal sealed class TableStyleElement
    {
        public ThemeShade? Fill { get; init; }

        public ThemeShade? FontColor { get; init; }

        public bool Bold { get; init; }

        public TableStyleLine Top { get; init; }

        public TableStyleLine Bottom { get; init; }

        public TableStyleLine Left { get; init; }

        public TableStyleLine Right { get; init; }

        public TableStyleLine Horizontal { get; init; }

        public TableStyleLine Vertical { get; init; }
    }
}
