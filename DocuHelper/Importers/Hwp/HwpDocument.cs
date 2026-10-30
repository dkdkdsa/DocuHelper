using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    public class HwpDocument
    {
        public HwpPage Page { get; set; }

        public List<HwpBlock> Blocks { get; set; } = new();

        public List<HwpNote> Notes { get; set; } = new();

        public HashSet<HwpSkipped> Skipped { get; set; } = new();
    }
}
