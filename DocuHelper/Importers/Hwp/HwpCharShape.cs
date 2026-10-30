namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpCharShape
    {
        private const int UnderlineBottom = 1;
        private const int UnderlineMiddle = 2;
        private const int UnderlineTop = 3;
        private const int SolidThreeDimensional = 15;

        public int FaceId { get; set; }

        public int BaseSize { get; set; }

        public uint Attributes { get; set; }

        public uint TextColor { get; set; }

        public bool Italic => (Attributes & 0x1) != 0;

        public bool Bold => (Attributes & 0x2) != 0;

        public bool Underline => UnderlineKind == UnderlineBottom || UnderlineKind == UnderlineTop;

        public bool Strike => ((Attributes & 0x1C0000) != 0 && StrikeShape != SolidThreeDimensional) || UnderlineKind == UnderlineMiddle;

        public bool Superscript => (Attributes & 0x8000) != 0;

        public bool Subscript => (Attributes & 0x10000) != 0;

        private int UnderlineKind => (int)((Attributes >> 2) & 0x3);

        private int StrikeShape => (int)((Attributes >> 26) & 0xF);

        public static HwpCharShape Read(HwpRecord record) => new()
        {
            FaceId = record.UInt16(0),
            BaseSize = record.Int32(42),
            Attributes = record.UInt32(46),
            TextColor = record.UInt32(52),
        };
    }
}
