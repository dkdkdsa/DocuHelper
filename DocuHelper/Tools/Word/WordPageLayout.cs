namespace DocuHelper.Tools.Word
{
    public sealed record WordPageLayout(int Width, int Height, int Top, int Right, int Bottom, int Left, double Capacity, int NormalFontSize)
    {
        public static WordPageLayout Default { get; } = new(11906, 16838, 1701, 1440, 1440, 1440, WordPager.LinesPerPage, 20);
    }
}
