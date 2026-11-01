using System;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsPalette
    {
        public const int FirstIndex = 8;
        public const int AutoForeground = 0x40;
        public const int AutoBackground = 0x41;

        private static readonly string[] s_default =
        {
            "000000", "FFFFFF", "FF0000", "00FF00", "0000FF", "FFFF00", "FF00FF", "00FFFF",
            "800000", "008000", "000080", "808000", "800080", "008080", "C0C0C0", "808080",
            "9999FF", "993366", "FFFFCC", "CCFFFF", "660066", "FF8080", "0066CC", "CCCCFF",
            "000080", "FF00FF", "FFFF00", "00FFFF", "800080", "800000", "008080", "0000FF",
            "00CCFF", "CCFFFF", "CCFFCC", "FFFF99", "99CCFF", "FF99CC", "CC99FF", "FFCC99",
            "3366FF", "33CCCC", "99CC00", "FFCC00", "FF9900", "FF6600", "666699", "969696",
            "003366", "339966", "003300", "333300", "993300", "993366", "333399", "333333",
        };

        private readonly string[] _colors = (string[])s_default.Clone();

        public void Read(XlsRecord record)
        {
            var reader = new XlsDataReader(record);
            var count = reader.ReadUInt16();
            for (var i = 0; i < count && i < _colors.Length && reader.Remaining >= 4; i++)
            {
                var red = reader.ReadByte();
                var green = reader.ReadByte();
                var blue = reader.ReadByte();
                reader.Skip(1);
                _colors[i] = red.ToString("X2") + green.ToString("X2") + blue.ToString("X2");
            }
        }

        public string Resolve(int index, bool forFill)
        {
            if (index >= 0 && index < FirstIndex)
                return "FF" + _colors[index];

            if (index >= FirstIndex && index < FirstIndex + _colors.Length)
                return "FF" + _colors[index - FirstIndex];

            if (!forFill)
                return null;

            return index == AutoBackground ? "FFFFFFFF" : index == AutoForeground ? "FF000000" : null;
        }
    }
}
