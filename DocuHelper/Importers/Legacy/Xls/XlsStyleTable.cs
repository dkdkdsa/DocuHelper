using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsStyleTable
    {
        public List<XlsFont> Fonts { get; } = new();

        public Dictionary<int, string> Formats { get; } = new();

        public List<XlsXf> Xfs { get; } = new();

        public XlsPalette Palette { get; } = new();

        public XlsFont FontAt(int index)
        {
            var slot = index > 4 ? index - 1 : index == 4 ? -1 : index;
            if (slot >= 0 && slot < Fonts.Count)
                return Fonts[slot];

            return Fonts.Count > 0 ? Fonts[0] : null;
        }

        public void ReadFont(XlsRecord record) => Fonts.Add(XlsFont.Read(record));

        public void ReadFormat(XlsRecord record)
        {
            var reader = new XlsDataReader(record);
            var id = reader.ReadUInt16();
            Formats[id] = reader.ReadLongString();
        }

        public void ReadXf(XlsRecord record) => Xfs.Add(XlsXf.Read(record));

        public void ReadXfExt(XlsRecord record)
        {
            var reader = new XlsDataReader(record);
            if (reader.Remaining < 20)
                return;

            reader.Skip(14);
            var index = reader.ReadUInt16();
            reader.Skip(2);
            var count = reader.ReadUInt16();
            if (index >= Xfs.Count)
                return;

            var colors = Xfs[index].Colors;
            for (var i = 0; i < count && reader.Remaining >= 4; i++)
            {
                var start = reader.Position;
                var type = reader.ReadUInt16();
                var size = reader.ReadUInt16();
                if (size < 4 || size > reader.Remaining + 4)
                    return;

                if (size >= 12 && (type == XlsFullColor.CellForeground || type == XlsFullColor.CellBackground || type == XlsFullColor.TextColor
                    || (type >= XlsFullColor.TopBorder && type <= XlsFullColor.DiagonalBorder)))
                    colors[type] = XlsFullColor.Read(reader);

                reader.Position = start + size;
            }
        }
    }
}
