using System.IO;
using DocuHelper.Importers.Hwp;
using DocuHelper.Importers.Legacy.Doc;
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

        public string AssistantRevisionAuthor { get; set; }

        public override void Load(byte[] content) =>
            base.Load(HwpCompoundFile.IsCompoundFile(content) ? DocReader.ToDocx(content) : content);

        public override byte[] ToBytes() => WordDocumentProperties.WithCoreProperties(base.ToBytes(), Document);

        protected override WordprocessingDocument Open(Stream stream) => WordprocessingDocument.Open(stream, true);

        protected override void OnLoaded() => WordIds.Prepare(Document);
    }
}
