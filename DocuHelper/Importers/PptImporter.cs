using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelper.Importers
{
    public class PptImporter : DocuImporter
    {
        public override IReadOnlyList<string> SupportedExtensions { get; } = [".pptx"];

        public override PptDocu Create(string filePath, byte[] content) => new(filePath, content);
    }
}
