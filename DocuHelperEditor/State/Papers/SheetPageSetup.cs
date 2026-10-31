using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SheetPageSetup
    {
        public SheetOrientation Orientation { get; set; }

        public int PaperSize { get; set; } = 9;

        public double MarginLeft { get; set; } = 1.78;

        public double MarginRight { get; set; } = 1.78;

        public double MarginTop { get; set; } = 1.91;

        public double MarginBottom { get; set; } = 1.91;

        public double MarginHeader { get; set; } = 0.76;

        public double MarginFooter { get; set; } = 0.76;

        public int? Scale { get; set; }

        public int? FitWidth { get; set; }

        public int? FitHeight { get; set; }

        public string PrintArea { get; set; }

        public string TitleRows { get; set; }

        public string TitleColumns { get; set; }

        public bool GridlinesPrint { get; set; }

        public bool HeadingsPrint { get; set; }

        public bool CenterHorizontally { get; set; }

        public bool CenterVertically { get; set; }

        public string HeaderLeft { get; set; }

        public string HeaderCenter { get; set; }

        public string HeaderRight { get; set; }

        public string FooterLeft { get; set; }

        public string FooterCenter { get; set; }

        public string FooterRight { get; set; }

        public bool DifferentFirst { get; set; }

        public List<int> RowBreaks { get; set; } = new();

        public List<int> ColumnBreaks { get; set; } = new();

        public string BackgroundUrl { get; set; }

        public bool HasHeaderFooter => !string.IsNullOrEmpty(HeaderLeft) || !string.IsNullOrEmpty(HeaderCenter) || !string.IsNullOrEmpty(HeaderRight)
            || !string.IsNullOrEmpty(FooterLeft) || !string.IsNullOrEmpty(FooterCenter) || !string.IsNullOrEmpty(FooterRight);

        public bool HasFitToPages => FitWidth.HasValue || FitHeight.HasValue;
    }
}
