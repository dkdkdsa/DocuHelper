using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocColors
    {
        private static readonly int[] s_palette =
        {
            -1, 0x000000, 0x0000FF, 0x00FFFF, 0x00FF00, 0xFF00FF, 0xFF0000, 0xFFFF00, 0xFFFFFF,
            0x000080, 0x008080, 0x008000, 0x800080, 0x800000, 0x808000, 0x808080, 0xC0C0C0,
        };

        private static readonly HighlightColorValues[] s_highlights =
        {
            HighlightColorValues.None, HighlightColorValues.Black, HighlightColorValues.Blue, HighlightColorValues.Cyan,
            HighlightColorValues.Green, HighlightColorValues.Magenta, HighlightColorValues.Red, HighlightColorValues.Yellow,
            HighlightColorValues.White, HighlightColorValues.DarkBlue, HighlightColorValues.DarkCyan, HighlightColorValues.DarkGreen,
            HighlightColorValues.DarkMagenta, HighlightColorValues.DarkRed, HighlightColorValues.DarkYellow,
            HighlightColorValues.DarkGray, HighlightColorValues.LightGray,
        };

        public static int FromIndex(int index) => index >= 0 && index < s_palette.Length ? s_palette[index] : -1;

        public static string Hex(int rgb) => (rgb & 0xFFFFFF).ToString("X6");

        public static HighlightColorValues? Highlight(int index) =>
            index > 0 && index < s_highlights.Length ? s_highlights[index] : null;
    }
}
