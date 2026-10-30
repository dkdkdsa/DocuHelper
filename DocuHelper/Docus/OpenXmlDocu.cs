using System.IO;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Docus
{
    public abstract class OpenXmlDocu<TPackage> : Docu where TPackage : OpenXmlPackage
    {
        private MemoryStream _stream;

        protected OpenXmlDocu(string filePath, byte[] content)
            : base(filePath)
        {
            Load(content);
        }

        public TPackage Document { get; private set; }

        public override byte[] ToBytes()
        {
            using var output = new MemoryStream();
            using (Document.Clone(output))
            {
            }

            return output.ToArray();
        }

        public override void Load(byte[] content)
        {
            Document?.Dispose();
            _stream?.Dispose();

            _stream = new MemoryStream();
            _stream.Write(content, 0, content.Length);
            _stream.Position = 0;
            Document = Open(_stream);
            OnLoaded();
        }

        public override void Dispose()
        {
            Document?.Dispose();
            _stream?.Dispose();
            Document = null;
            _stream = null;
        }

        protected abstract TPackage Open(Stream stream);

        protected virtual void OnLoaded()
        {
        }
    }
}
