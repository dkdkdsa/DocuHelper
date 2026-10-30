namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpParaShape
    {
        public const int HeadingOutline = 1;
        public const int HeadingNumber = 2;
        public const int HeadingBullet = 3;

        private const uint LineSpacingVersion = 0x05000205;
        private const int PercentSpacing = 0;

        public uint Attributes { get; set; }

        public int LeftMargin { get; set; }

        public int Indent { get; set; }

        public int SpaceBefore { get; set; }

        public int SpaceAfter { get; set; }

        public int LineSpacing { get; set; }

        public int HeadingId { get; set; }

        public int Level { get; set; }

        public int HeadingType => (int)((Attributes >> 23) & 0x3);

        public bool PageBreakBefore => (Attributes & 0x80000) != 0;

        public HwpAlignment Alignment => ((Attributes >> 2) & 0x7) switch
        {
            1 => HwpAlignment.Left,
            2 => HwpAlignment.Right,
            3 => HwpAlignment.Center,
            4 => HwpAlignment.Distribute,
            5 => HwpAlignment.Distribute,
            _ => HwpAlignment.Justify,
        };

        public static HwpParaShape Read(HwpRecord record, uint version)
        {
            var attributes = record.UInt32(0);
            var modern = version >= LineSpacingVersion && record.Has(46, 8);
            var spacingType = modern ? (int)(record.UInt32(46) & 0x1F) : (int)(attributes & 0x3);
            var spacing = modern ? record.Int32(50) : record.Int32(24);
            return new HwpParaShape
            {
                Attributes = attributes,
                LeftMargin = record.Int32(4),
                Indent = record.Int32(12),
                SpaceBefore = record.Int32(16),
                SpaceAfter = record.Int32(20),
                LineSpacing = spacingType == PercentSpacing ? spacing : 0,
                HeadingId = record.UInt16(30),
                Level = record.Has(54, 4) ? (int)record.UInt32(54) : (int)((attributes >> 25) & 0x7),
            };
        }
    }
}
