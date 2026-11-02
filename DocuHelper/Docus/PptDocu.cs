using System.IO;
using DocuHelper.Importers.Hwp;
using DocuHelper.Importers.Legacy.Ppt;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Docus
{
    public class PptDocu : OpenXmlDocu<PresentationDocument>
    {
        public PptDocu(string filePath, byte[] content)
            : base(filePath, content)
        {
        }

        public override DocuType Type => DocuType.Ppt;

        public override void Load(byte[] content) =>
            base.Load(HwpCompoundFile.IsCompoundFile(content) ? PptBinaryReader.ToPptx(content) : content);

        protected override PresentationDocument Open(Stream stream) => PresentationDocument.Open(stream, true);
    }
}
