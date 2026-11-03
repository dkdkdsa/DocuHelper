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

        public Docu Import(string filePath) => Create(filePath, File.ReadAllBytes(filePath));

        public abstract Docu Create(string filePath, byte[] content);

        public static string ExplainUnsupported(string filePath) =>
            "엑셀, 워드, 파워포인트, 한글 파일(.xlsx, .xls, .docx, .doc, .pptx, .ppt, .hwp, .hwpx)과 대시보드(.dash)만 넣을 수 있어요.";
    }
}
