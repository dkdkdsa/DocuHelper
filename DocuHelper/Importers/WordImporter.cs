using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelper.Importers
{
    public class WordImporter : DocuImporter
    {
        public override IReadOnlyList<string> SupportedExtensions { get; } = [".docx", ".doc"];

        public override WordDocu Create(string filePath, byte[] content) => new(filePath, content);
    }
}
