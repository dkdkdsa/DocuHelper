using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    public class HwpConversion
    {
        public byte[] Content { get; set; }

        public HashSet<HwpSkipped> Skipped { get; set; } = new();
    }
}
