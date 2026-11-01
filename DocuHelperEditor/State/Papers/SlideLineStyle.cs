using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SlideLineStyle
    {
        public double FontCqw { get; set; }

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public string Color { get; set; }

        public string Bullet { get; set; } = "none";

        public string BulletText { get; set; }

        public string BulletColor { get; set; }

        public string BulletFont { get; set; }

        public double BulletSizePct { get; set; } = 100;

        public double IndentCqw { get; set; }

        public double MarginCqw { get; set; }

        public double FirstIndentCqw { get; set; }

        public double LineHeight { get; set; } = 1.2;

        public double LineHeightCqw { get; set; }

        public double SpaceBeforeCqw { get; set; }

        public double SpaceAfterCqw { get; set; }

        public bool FirstOfParagraph { get; set; } = true;

        public bool LastOfParagraph { get; set; } = true;

        public SlideTextAlign Align { get; set; }

        public List<SlideRunStyle> Runs { get; set; }
    }
}
