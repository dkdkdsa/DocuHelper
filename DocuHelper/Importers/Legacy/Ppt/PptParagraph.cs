using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptParagraph
    {
        public int Level { get; set; }

        public PptParaProps Props { get; set; }

        public PptCharProps EndProps { get; set; }

        public List<PptRun> Runs { get; } = new();
    }
}
