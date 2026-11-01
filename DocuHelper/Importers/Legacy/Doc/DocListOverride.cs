using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocListOverride
    {
        public long ListId { get; set; }

        public List<DocLevelOverride> Levels { get; } = new();
    }
}
