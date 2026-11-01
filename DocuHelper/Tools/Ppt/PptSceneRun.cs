namespace DocuHelper.Tools.Ppt
{
    public class PptSceneRun
    {
        public string Text { get; set; }

        public string FontLatin { get; set; }

        public string FontEa { get; set; }

        public double? SizePt { get; set; }

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public bool Strike { get; set; }

        public string Color { get; set; }

        public bool ColorExplicit { get; set; }

        public string Highlight { get; set; }

        public double Baseline { get; set; }

        public double SpacingPt { get; set; }

        public PptSceneLink Link { get; set; }

        public string FieldType { get; set; }

        public bool IsBreak { get; set; }
    }
}
