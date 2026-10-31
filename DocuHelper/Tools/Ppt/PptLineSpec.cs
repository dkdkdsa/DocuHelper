namespace DocuHelper.Tools.Ppt
{
    public sealed class PptLineSpec
    {
        public PptLineSpec(PptColorSpec color, double? widthPoints, string dash)
        {
            Color = color;
            WidthPoints = widthPoints;
            Dash = dash;
        }

        public PptColorSpec Color { get; }

        public double? WidthPoints { get; }

        public string Dash { get; }

        public bool IsEmpty => Color == null && WidthPoints == null && Dash == null;
    }
}
