using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocListDefinition
    {
        public long Id { get; set; }

        public List<DocListLevel> Levels { get; } = new();
    }
}
