using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelper.Importers
{
    public class HwpImporter : DocuImporter
    {
        public override IReadOnlyList<string> SupportedExtensions { get; } = [".hwp", ".hwpx"];

        public override HwpDocu Create(string filePath, byte[] content) => new(filePath, content);
    }
}
