namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpNumbering
    {
        public const int LevelCount = 10;

        private const int BaseLevelCount = 7;
        private const int HeadInfoSize = 12;
        private const uint LevelStartVersion = 0x05000205;

        public string[] Formats { get; } = new string[LevelCount];

        public int[] Shapes { get; } = new int[LevelCount];

        public int[] Starts { get; } = new int[LevelCount];

        public static HwpNumbering Read(HwpRecord record, uint version)
        {
            var numbering = new HwpNumbering();
            var at = 0;
            for (var level = 0; level < BaseLevelCount; level++)
                at = numbering.ReadLevel(record, at, level);

            var start = record.UInt16(at);
            at += 2;
            for (var level = 0; level < LevelCount; level++)
                numbering.Starts[level] = 1;

            if (start > 0)
                numbering.Starts[0] = start;

            if (version < LevelStartVersion || !record.Has(at, BaseLevelCount * 4))
                return numbering;

            for (var level = 0; level < BaseLevelCount; level++, at += 4)
                numbering.Starts[level] = (int)record.UInt32(at);

            for (var level = BaseLevelCount; level < LevelCount && record.Has(at, HeadInfoSize + 2); level++)
                at = numbering.ReadLevel(record, at, level);

            for (var level = BaseLevelCount; level < LevelCount && record.Has(at, 4); level++, at += 4)
                numbering.Starts[level] = (int)record.UInt32(at);

            return numbering;
        }

        private int ReadLevel(HwpRecord record, int at, int level)
        {
            Shapes[level] = (int)((record.UInt32(at) >> 5) & 0x1F);
            Formats[level] = record.HwpString(at + HeadInfoSize, out var next);
            return next;
        }
    }
}
