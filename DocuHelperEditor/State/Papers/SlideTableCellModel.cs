using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SlideTableCellModel
    {
        public List<string> Lines { get; set; } = new();

        public string FillColor { get; set; }

        public string TextColor { get; set; }

        public string InheritedColor { get; set; }

        public bool Bold { get; set; }

        public double FontCqw { get; set; }

        public SlideTextAlign Align { get; set; }

        public string VerticalAlign { get; set; } = "t";

        public int GridSpan { get; set; } = 1;

        public int RowSpan { get; set; } = 1;

        public bool Merged { get; set; }

        public string BorderTop { get; set; }

        public string BorderRight { get; set; }

        public string BorderBottom { get; set; }

        public string BorderLeft { get; set; }

        public double InsetLeft { get; set; }

        public double InsetTop { get; set; }

        public double InsetRight { get; set; }

        public double InsetBottom { get; set; }

        public List<SlideLineStyle> LineStyles { get; set; }
    }
}
