using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptProperties
    {
        public const int Rotation = 0x0004;
        public const int Pib = 0x0104;
        public const int CropTop = 0x0100;
        public const int CropBottom = 0x0101;
        public const int CropLeft = 0x0102;
        public const int CropRight = 0x0103;
        public const int TextLeft = 0x0081;
        public const int TextTop = 0x0082;
        public const int TextRight = 0x0083;
        public const int TextBottom = 0x0084;
        public const int WrapText = 0x0085;
        public const int AnchorText = 0x0087;
        public const int TextFlow = 0x0088;
        public const int TextBooleans = 0x00BF;
        public const int FillType = 0x0180;
        public const int FillColor = 0x0181;
        public const int FillOpacity = 0x0182;
        public const int FillBackColor = 0x0183;
        public const int FillBooleans = 0x01BF;
        public const int LineColor = 0x01C0;
        public const int LineOpacity = 0x01C1;
        public const int LineWidth = 0x01CB;
        public const int LineDashing = 0x01CD;
        public const int LineStartArrow = 0x01D0;
        public const int LineEndArrow = 0x01D1;
        public const int LineBooleans = 0x01FF;

        private const int MaxProperties = 4096;

        private readonly Dictionary<int, uint> _values = new();

        public PptProperties Parent { get; set; }

        public void Read(PptRecord record)
        {
            var count = record.Instance;
            for (var i = 0; i < count && i < MaxProperties && record.Has(i * 6, 6); i++)
            {
                var id = record.UInt16(i * 6) & 0x3FFF;
                _values[id] = record.UInt32(i * 6 + 2);
            }
        }

        public bool HasOwn(int id) => _values.ContainsKey(id);

        public bool Has(int id) => _values.ContainsKey(id) || (Parent != null && Parent.Has(id));

        public uint Get(int id, uint fallback)
        {
            if (_values.TryGetValue(id, out var value))
                return value;

            return Parent != null ? Parent.Get(id, fallback) : fallback;
        }

        public int GetInt(int id, int fallback) => (int)Get(id, (uint)fallback);

        public bool? FlagOwn(int id, int bit)
        {
            if (_values.TryGetValue(id, out var value) && (value & ((uint)bit << 16)) != 0)
                return (value & (uint)bit) != 0;

            return null;
        }

        public bool? Flag(int id, int bit)
        {
            if (_values.TryGetValue(id, out var value) && (value & ((uint)bit << 16)) != 0)
                return (value & (uint)bit) != 0;

            return Parent?.Flag(id, bit);
        }
    }
}
