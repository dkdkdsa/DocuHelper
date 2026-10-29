namespace DocuHelper.Docus
{
    public class WordDocu(string filePath) : Docu(filePath)
    {
        public override DocuType Type => DocuType.Word;
    }
}
