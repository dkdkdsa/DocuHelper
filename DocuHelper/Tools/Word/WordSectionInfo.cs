namespace DocuHelper.Tools.Word
{
    public sealed class WordSectionInfo
    {
        public int Number { get; init; }

        public int FirstBlock { get; init; }

        public int LastBlock { get; init; }

        public int FirstPage { get; init; }

        public int LastPage { get; init; }

        public string BreakType { get; init; }

        public double? WidthCm { get; init; }

        public double? HeightCm { get; init; }

        public string Paper { get; init; }

        public bool Landscape { get; init; }

        public double? TopCm { get; init; }

        public double? BottomCm { get; init; }

        public double? LeftCm { get; init; }

        public double? RightCm { get; init; }

        public double? HeaderCm { get; init; }

        public double? FooterCm { get; init; }

        public double? GutterCm { get; init; }

        public int Columns { get; init; } = 1;

        public double? ColumnSpaceCm { get; init; }

        public bool ColumnLine { get; init; }

        public bool ColumnsEqual { get; init; } = true;

        public string LineNumbers { get; init; }

        public string PageNumberFormat { get; init; }

        public int? PageNumberStart { get; init; }

        public string VerticalAlign { get; init; }

        public bool TitlePage { get; init; }

        public bool PageBorders { get; init; }

        public string Anchor => WordAnchors.Section(Number);
    }
}
