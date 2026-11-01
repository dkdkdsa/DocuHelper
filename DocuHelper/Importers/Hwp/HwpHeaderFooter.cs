using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    public class HwpHeaderFooter
    {
        public bool Footer { get; set; }

        public string Apply { get; set; } = "BOTH";

        public List<HwpBlock> Blocks { get; set; } = new();
    }
}
