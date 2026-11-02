using System.IO;
using DocuHelper.Importers.Hwp;
using DocuHelper.Importers.Legacy.Xls;
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

        public override void Load(byte[] content) =>
            base.Load(HwpCompoundFile.IsCompoundFile(content) ? XlsBinaryReader.ToXlsx(content) : content);

        protected override SpreadsheetDocument Open(Stream stream) => SpreadsheetDocument.Open(stream, true);
    }
}
