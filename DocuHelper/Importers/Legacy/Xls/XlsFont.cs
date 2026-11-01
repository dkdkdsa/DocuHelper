namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsFont
    {
        public int Height { get; set; }

        public bool Italic { get; set; }

        public bool Strike { get; set; }

        public int ColorIndex { get; set; }

        public int Weight { get; set; }

        public int Script { get; set; }

        public int Underline { get; set; }

        public int Family { get; set; }

        public int CharSet { get; set; }

        public string Name { get; set; }

        public static XlsFont Read(XlsRecord record)
        {
            var reader = new XlsDataReader(record);
            var font = new XlsFont { Height = reader.ReadUInt16() };
            var flags = reader.ReadUInt16();
            font.Italic = (flags & 0x02) != 0;
            font.Strike = (flags & 0x08) != 0;
            font.ColorIndex = reader.ReadUInt16();
            font.Weight = reader.ReadUInt16();
            font.Script = reader.ReadUInt16();
            font.Underline = reader.ReadByte();
            font.Family = reader.ReadByte();
            font.CharSet = reader.ReadByte();
            reader.Skip(1);
            font.Name = reader.ReadShortString();
            return font;
        }
    }
}
