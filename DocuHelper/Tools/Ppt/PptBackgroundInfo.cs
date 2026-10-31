namespace DocuHelper.Tools.Ppt
{
    public class PptBackgroundInfo
    {
        public string Kind { get; set; } = "none";

        public string Color { get; set; }

        public string Color2 { get; set; }

        public double AngleDeg { get; set; }

        public int Alpha { get; set; } = 100;

        public string ImageKey { get; set; }

        public bool HideMasterShapes { get; set; }
    }
}
