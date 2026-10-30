using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    public class HwpNote
    {
        public string Mark { get; set; }

        public List<HwpBlock> Blocks { get; set; } = new();
    }
}
