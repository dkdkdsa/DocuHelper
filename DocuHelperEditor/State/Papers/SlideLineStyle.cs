namespace DocuHelperEditor.State.Papers
{
    public class SlideLineStyle
    {
        public double FontCqw { get; set; }

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public string Color { get; set; }

        public string Bullet { get; set; } = "none";

        public double IndentCqw { get; set; }

        public SlideTextAlign Align { get; set; }
    }
}
