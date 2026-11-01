namespace DocuHelper.Importers.Hwp
{
    public class HwpPicture
    {
        public int Index { get; set; }

        public byte[] Bytes { get; set; }

        public string Extension { get; set; }

        public string ContentType { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public string AltText { get; set; }
    }
}
