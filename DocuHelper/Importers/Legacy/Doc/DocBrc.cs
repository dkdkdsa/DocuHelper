namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocBrc
    {
        public int Type { get; set; }

        public int Width { get; set; }

        public int Color { get; set; } = -1;

        public int Space { get; set; }

        public bool IsSet => Type != 0 && Type != 255 && Width > 0;

        public static DocBrc Read80(byte[] data, int offset) => new DocBrc
        {
            Width = data[offset],
            Type = data[offset + 1],
            Color = DocColors.FromIndex(data[offset + 2]),
            Space = data[offset + 3] & 0x1F,
        };

        public static DocBrc Read(byte[] data, int offset) => new DocBrc
        {
            Color = data[offset + 3] == 0xFF ? -1 : (data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2],
            Width = data[offset + 4],
            Type = data[offset + 5],
            Space = data[offset + 6] & 0x1F,
        };
    }
}
