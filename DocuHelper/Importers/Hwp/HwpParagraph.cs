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
    }
}
