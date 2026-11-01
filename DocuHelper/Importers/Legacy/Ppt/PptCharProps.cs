namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptCharProps
    {
        public bool? Bold { get; set; }

        public bool? Italic { get; set; }

        public bool? Underline { get; set; }

        public int? Font { get; set; }

        public int? EastAsianFont { get; set; }

        public int? Size { get; set; }

        public uint? Color { get; set; }

        public int? Position { get; set; }

        public static PptCharProps Read(PptRecord record, ref int at)
        {
            var props = new PptCharProps();
            var masks = record.UInt32(at);
            at += 4;
            if ((masks & 0xFFFF) != 0)
            {
                var flags = record.UInt16(at);
                at += 2;
                if ((masks & 0x1) != 0)
                    props.Bold = (flags & 0x1) != 0;

                if ((masks & 0x2) != 0)
                    props.Italic = (flags & 0x2) != 0;

                if ((masks & 0x4) != 0)
                    props.Underline = (flags & 0x4) != 0;
            }

            if ((masks & 0x10000) != 0)
            {
                props.Font = record.UInt16(at);
                at += 2;
            }

            if ((masks & 0x200000) != 0)
            {
                props.EastAsianFont = record.UInt16(at);
                at += 2;
            }

            if ((masks & 0x400000) != 0)
                at += 2;

            if ((masks & 0x800000) != 0)
                at += 2;

            if ((masks & 0x20000) != 0)
            {
                props.Size = record.UInt16(at);
                at += 2;
            }

            if ((masks & 0x40000) != 0)
            {
                props.Color = record.UInt32(at);
                at += 4;
            }

            if ((masks & 0x80000) != 0)
            {
                props.Position = record.Int16(at);
                at += 2;
            }

            return props;
        }

        public bool SameAs(PptCharProps other) =>
            other != null && Bold == other.Bold && Italic == other.Italic && Underline == other.Underline && Font == other.Font
            && EastAsianFont == other.EastAsianFont && Size == other.Size && Color == other.Color && Position == other.Position;

        public PptCharProps With(PptCharProps top)
        {
            if (top == null)
                return this;

            return new PptCharProps
            {
                Bold = top.Bold ?? Bold,
                Italic = top.Italic ?? Italic,
                Underline = top.Underline ?? Underline,
                Font = top.Font ?? Font,
                EastAsianFont = top.EastAsianFont ?? EastAsianFont,
                Size = top.Size ?? Size,
                Color = top.Color ?? Color,
                Position = top.Position ?? Position,
            };
        }
    }
}
