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

        public static string ExplainUnsupported(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".hwp" or ".hwpx" => "한글 파일은 아직 못 열어요. 한글에서 ‘다른 이름으로 저장’으로 .docx를 골라 저장한 다음 넣어 주세요.",
            ".xls" or ".doc" or ".ppt" => "옛날 형식 파일은 아직 못 열어요. 오피스에서 ‘다른 이름으로 저장’으로 새 형식(.xlsx, .docx, .pptx)을 골라 저장한 다음 넣어 주세요.",
            _ => "엑셀, 워드, 파워포인트 파일(.xlsx, .docx, .pptx)만 넣을 수 있어요.",
        };
    }
}
