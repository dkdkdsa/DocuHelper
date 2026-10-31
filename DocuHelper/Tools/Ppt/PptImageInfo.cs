namespace DocuHelper.Tools.Ppt
{
    public class PptImageInfo
    {
        public PptImageType Type { get; set; }

        public string Mime { get; set; }

        public string Extension { get; set; }

        public int WidthPx { get; set; }

        public int HeightPx { get; set; }

        public long ByteCount { get; set; }
    }
}
