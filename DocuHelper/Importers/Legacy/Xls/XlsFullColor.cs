using System;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsFullColor
    {
        public const int CellForeground = 4;
        public const int CellBackground = 5;
        public const int TopBorder = 7;
        public const int BottomBorder = 8;
        public const int LeftBorder = 9;
        public const int RightBorder = 10;
        public const int DiagonalBorder = 11;
        public const int TextColor = 13;

        private const int Indexed = 1;
        private const int Rgb = 2;
        private const int Themed = 3;

        public int Type { get; private set; }

        public double Tint { get; private set; }

        public uint Value { get; private set; }

        public static XlsFullColor Read(XlsDataReader reader)
        {
            var color = new XlsFullColor { Type = reader.ReadUInt16() };
            var tint = reader.ReadInt16();
            color.Tint = Math.Clamp(tint / 32767.0, -1, 1);
            color.Value = reader.ReadUInt32();
            return color;
        }

        public T ToColor<T>(XlsPalette palette)
            where T : ColorType, new()
        {
            var color = new T();
            switch (Type)
            {
                case Rgb:
                    color.Rgb = "FF" + (Value & 0xFF).ToString("X2") + ((Value >> 8) & 0xFF).ToString("X2") + ((Value >> 16) & 0xFF).ToString("X2");
                    break;
                case Themed when Value <= 11:
                    color.Theme = Value;
                    break;
                case Indexed when palette.Resolve((int)Value, true) is string hex:
                    color.Rgb = hex;
                    break;
                default:
                    return null;
            }

            if (Tint != 0)
                color.Tint = Math.Round(Tint, 6);

            return color;
        }
    }
}
