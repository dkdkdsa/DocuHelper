namespace DocuHelperEditor.State.Papers
{
    public class SheetVisual
    {
        public string FillColor { get; set; }

        public string FontColor { get; set; }

        public bool? Bold { get; set; }

        public bool? Italic { get; set; }

        public bool? Underline { get; set; }

        public bool Strike { get; set; }

        public string Border { get; set; }

        public double? BarFraction { get; set; }

        public string BarColor { get; set; }

        public string ScaleFill { get; set; }

        public string IconSet { get; set; }

        public int? IconIndex { get; set; }

        public int IconCount { get; set; }

        public bool HasBar => BarFraction.HasValue && !string.IsNullOrEmpty(BarColor);

        public bool HasIcon => !string.IsNullOrEmpty(IconSet) && IconIndex.HasValue;
    }
}
