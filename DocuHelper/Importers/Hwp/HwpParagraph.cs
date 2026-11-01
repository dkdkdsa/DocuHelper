using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    public class HwpParagraph : HwpBlock
    {
        public List<HwpRun> Runs { get; set; } = new();

        public string Label { get; set; }

        public HwpAlignment Alignment { get; set; }

        public int LeftIndent { get; set; }

        public int FirstLineIndent { get; set; }

        public int SpaceBefore { get; set; }

        public int SpaceAfter { get; set; }

        public int LineSpacing { get; set; }

        public bool PageBreakBefore { get; set; }

        public bool ColumnBreakBefore { get; set; }

        public string PageHiding { get; set; }

        public int? NewPageNumber { get; set; }

        public int RightIndent { get; set; }

        public bool KeepWithNext { get; set; }

        public bool KeepLines { get; set; }

        public bool WidowControl { get; set; }

        public string LineSpacingKind { get; set; } = "PERCENT";

        internal HwpxBorderFill Border { get; set; }

        public List<HwpTabStop> Tabs { get; set; } = new();

        public string StyleName { get; set; }
    }
}
