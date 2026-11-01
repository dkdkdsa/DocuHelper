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
    }
}
