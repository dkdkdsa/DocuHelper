namespace DocuHelper.Importers.Legacy.Ppt
{
    internal readonly struct PptRect
    {
        public PptRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left { get; }

        public int Top { get; }

        public int Right { get; }

        public int Bottom { get; }

        public long Width => (long)Right - Left;

        public long Height => (long)Bottom - Top;
    }
}
