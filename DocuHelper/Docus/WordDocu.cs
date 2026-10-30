using System.IO;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Docus
{
    public class WordDocu : OpenXmlDocu<WordprocessingDocument>
    {
        public WordDocu(string filePath, byte[] content)
            : base(filePath, content)
        {
        }

        public override DocuType Type => DocuType.Word;

        protected override WordprocessingDocument Open(Stream stream) => WordprocessingDocument.Open(stream, true);

        protected override void OnLoaded() => WordIds.Prepare(Document);
    }
}
