using System.IO;
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

        protected override PresentationDocument Open(Stream stream) => PresentationDocument.Open(stream, true);
    }
}
