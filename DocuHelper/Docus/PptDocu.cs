namespace DocuHelper.Docus
{
    public class PptDocu(string filePath) : Docu(filePath)
    {
        public override DocuType Type => DocuType.Ppt;
    }
}
