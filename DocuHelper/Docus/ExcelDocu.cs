using System.IO;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Docus
{
    public class ExcelDocu : OpenXmlDocu<SpreadsheetDocument>
    {
        public ExcelDocu(string filePath, byte[] content)
            : base(filePath, content)
        {
        }

        public override DocuType Type => DocuType.Excel;

        protected override SpreadsheetDocument Open(Stream stream) => SpreadsheetDocument.Open(stream, true);
    }
}
