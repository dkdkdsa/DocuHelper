namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptParaProps
    {
        public bool? HasBullet { get; set; }

        public int? BulletChar { get; set; }

        public int? BulletFont { get; set; }

        public int? BulletSize { get; set; }

        public uint? BulletColor { get; set; }

        public int? Align { get; set; }

        public int? LineSpacing { get; set; }

        public int? SpaceBefore { get; set; }

        public int? SpaceAfter { get; set; }

        public int? LeftMargin { get; set; }

        public int? Indent { get; set; }

        public static PptParaProps Read(PptRecord record, ref int at)
        {
            var props = new PptParaProps();
            var masks = record.UInt32(at);
            at += 4;
            if ((masks & 0xF) != 0)
            {
                var flags = record.UInt16(at);
                at += 2;
                if ((masks & 0x1) != 0)
                    props.HasBullet = (flags & 0x1) != 0;
            }

            if ((masks & 0x80) != 0)
            {
                props.BulletChar = record.UInt16(at);
                at += 2;
            }

            if ((masks & 0x10) != 0)
            {
                props.BulletFont = record.UInt16(at);
                at += 2;
            }

            if ((masks & 0x40) != 0)
            {
                props.BulletSize = record.Int16(at);
                at += 2;
            }

            if ((masks & 0x20) != 0)
            {
                props.BulletColor = record.UInt32(at);
                at += 4;
            }

            props.Align = ReadShort(record, masks, 0x800, ref at);
            props.LineSpacing = ReadShort(record, masks, 0x1000, ref at);
            props.SpaceBefore = ReadShort(record, masks, 0x2000, ref at);
            props.SpaceAfter = ReadShort(record, masks, 0x4000, ref at);
            props.LeftMargin = ReadShort(record, masks, 0x100, ref at);
            props.Indent = ReadShort(record, masks, 0x400, ref at);
            ReadShort(record, masks, 0x8000, ref at);
            if ((masks & 0x100000) != 0)
                at += 2 + record.UInt16(at) * 4;

            ReadShort(record, masks, 0x10000, ref at);
            if ((masks & 0xE0000) != 0)
                at += 2;

            ReadShort(record, masks, 0x200000, ref at);
            return props;
        }

        public PptParaProps With(PptParaProps top)
        {
            if (top == null)
                return this;

            return new PptParaProps
            {
                HasBullet = top.HasBullet ?? HasBullet,
                BulletChar = top.BulletChar ?? BulletChar,
                BulletFont = top.BulletFont ?? BulletFont,
                BulletSize = top.BulletSize ?? BulletSize,
                BulletColor = top.BulletColor ?? BulletColor,
                Align = top.Align ?? Align,
                LineSpacing = top.LineSpacing ?? LineSpacing,
                SpaceBefore = top.SpaceBefore ?? SpaceBefore,
                SpaceAfter = top.SpaceAfter ?? SpaceAfter,
                LeftMargin = top.LeftMargin ?? LeftMargin,
                Indent = top.Indent ?? Indent,
            };
        }

        private static int? ReadShort(PptRecord record, uint masks, uint bit, ref int at)
        {
            if ((masks & bit) == 0)
                return null;

            var value = (int)record.Int16(at);
            at += 2;
            return value;
        }
    }
}
