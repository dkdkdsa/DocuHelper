using System;
using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelper.Importers
{
    public class ExcelImporter : DocuImporter
    {
        public override IReadOnlyList<string> SupportedExtensions { get; } = [".xlsx"];

        public override ExcelDocu Import(string filePath)
        {
            throw new NotImplementedException();
        }
    }
}
