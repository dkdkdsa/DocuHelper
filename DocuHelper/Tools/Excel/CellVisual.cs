namespace DocuHelper.Tools.Excel
{
    public sealed class CellVisual
    {
        public string Fill { get; set; }

        public string FontColor { get; set; }

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public bool Strike { get; set; }

        public string Border { get; set; }

        public double? BarFraction { get; set; }

        public string BarColor { get; set; }

        public string ScaleFill { get; set; }

        public string IconSet { get; set; }

        public int IconIndex { get; set; } = -1;

        public int IconCount { get; set; }

        public bool HideValue { get; set; }

        public bool HasIcon => IconSet != null && IconIndex >= 0;

        public bool IsEmpty =>
            Fill == null && FontColor == null && !Bold && !Italic && !Underline && !Strike && Border == null
            && BarFraction == null && ScaleFill == null && !HasIcon;
    }
}
