namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsXf
    {
        public int FontIndex { get; set; }

        public int FormatId { get; set; }

        public bool Locked { get; set; }

        public bool Hidden { get; set; }

        public int Horizontal { get; set; }

        public int Vertical { get; set; }

        public bool Wrap { get; set; }

        public int Rotation { get; set; }

        public int Indent { get; set; }

        public bool Shrink { get; set; }

        public int LeftStyle { get; set; }

        public int RightStyle { get; set; }

        public int TopStyle { get; set; }

        public int BottomStyle { get; set; }

        public int DiagonalStyle { get; set; }

        public int LeftColor { get; set; }

        public int RightColor { get; set; }

        public int TopColor { get; set; }

        public int BottomColor { get; set; }

        public int DiagonalColor { get; set; }

        public int DiagonalDirection { get; set; }

        public int Pattern { get; set; }

        public int ForeColor { get; set; }

        public int BackColor { get; set; }

        public static XlsXf Read(XlsRecord record)
        {
            var reader = new XlsDataReader(record);
            var xf = new XlsXf { FontIndex = reader.ReadUInt16(), FormatId = reader.ReadUInt16() };
            var flags = reader.ReadUInt16();
            xf.Locked = (flags & 0x01) != 0;
            xf.Hidden = (flags & 0x02) != 0;
            var alignment = reader.ReadByte();
            xf.Horizontal = alignment & 0x07;
            xf.Wrap = (alignment & 0x08) != 0;
            xf.Vertical = (alignment >> 4) & 0x07;
            xf.Rotation = reader.ReadByte();
            var indent = reader.ReadByte();
            xf.Indent = indent & 0x0F;
            xf.Shrink = (indent & 0x10) != 0;
            reader.Skip(1);
            var styles = reader.ReadUInt16();
            xf.LeftStyle = styles & 0x0F;
            xf.RightStyle = (styles >> 4) & 0x0F;
            xf.TopStyle = (styles >> 8) & 0x0F;
            xf.BottomStyle = (styles >> 12) & 0x0F;
            var sideColors = reader.ReadUInt16();
            xf.LeftColor = sideColors & 0x7F;
            xf.RightColor = (sideColors >> 7) & 0x7F;
            xf.DiagonalDirection = (sideColors >> 14) & 0x03;
            var more = reader.ReadUInt32();
            xf.TopColor = (int)(more & 0x7F);
            xf.BottomColor = (int)((more >> 7) & 0x7F);
            xf.DiagonalColor = (int)((more >> 14) & 0x7F);
            xf.DiagonalStyle = (int)((more >> 21) & 0x0F);
            xf.Pattern = (int)((more >> 26) & 0x3F);
            var fill = reader.ReadUInt16();
            xf.ForeColor = fill & 0x7F;
            xf.BackColor = (fill >> 7) & 0x7F;
            return xf;
        }
    }
}
