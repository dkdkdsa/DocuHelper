namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptImage
    {
        public PptImage(byte[] bytes, string mime)
        {
            Bytes = bytes;
            Mime = mime;
        }

        public byte[] Bytes { get; }

        public string Mime { get; }
    }
}
