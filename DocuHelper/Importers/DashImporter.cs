using System.Collections.Generic;
using DocuHelper.Dash;
using DocuHelper.Docus;

namespace DocuHelper.Importers
{
    public class DashImporter : DocuImporter
    {
        public override IReadOnlyList<string> SupportedExtensions { get; } = [DashFile.Extension];

        public override DashDocu Create(string filePath, byte[] content) => new(filePath, content);
    }
}
