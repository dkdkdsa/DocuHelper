namespace DocuHelper.Tools.Ppt
{
    public sealed class PptFillSpec
    {
        public PptFillSpec(PptFillType type, PptColorSpec color = null, PptColorSpec color2 = null, double? angle = null, double? transparency = null)
        {
            Type = type;
            Color = color;
            Color2 = color2;
            Angle = angle;
            Transparency = transparency;
        }

        public PptFillType Type { get; }

        public PptColorSpec Color { get; }

        public PptColorSpec Color2 { get; }

        public double? Angle { get; }

        public double? Transparency { get; }
    }
}
