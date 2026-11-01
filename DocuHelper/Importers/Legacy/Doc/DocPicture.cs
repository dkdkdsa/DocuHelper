namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocPicture
    {
        public byte[] Bytes { get; set; }

        public string ContentType { get; set; }

        public long WidthEmu { get; set; }

        public long HeightEmu { get; set; }
    }
}
