namespace DocuHelper.Tools.Ppt
{
    public class PptFitResult
    {
        public bool IsKnown { get; set; }

        public bool Fits { get; set; }

        public int LinesNeeded { get; set; }

        public int LinesAvailable { get; set; }

        public double Ratio { get; set; }

        public double FontSize { get; set; }

        public bool AutoShrink { get; set; }

        public bool TooWide { get; set; }
    }
}
