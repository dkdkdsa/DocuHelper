using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocuHelper.Docus;

namespace DocuHelper.Importers
{
    public abstract class DocuImporter
    {
        public abstract IReadOnlyList<string> SupportedExtensions { get; }

        public bool CanImport(string filePath) =>
            SupportedExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);

        public abstract Docu Import(string filePath);
    }
}
