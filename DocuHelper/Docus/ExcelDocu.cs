namespace DocuHelper.Docus
{
    public class ExcelDocu(string filePath) : Docu(filePath)
    {
        public override DocuType Type => DocuType.Excel;
    }
}
