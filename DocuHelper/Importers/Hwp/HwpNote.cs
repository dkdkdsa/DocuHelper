using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    public class HwpNote
    {
        public int Id { get; set; }

        public bool Endnote { get; set; }

        public List<HwpBlock> Blocks { get; set; } = new();
    }
}
