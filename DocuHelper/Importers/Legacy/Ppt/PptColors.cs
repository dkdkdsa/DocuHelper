using System.Globalization;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal static class PptColors
    {
        public const int SchemeCount = 8;

        public static uint[] FindScheme(PptRecord container)
        {
            if (container == null)
                return null;

            PptRecord first = null;
            foreach (var child in container.Children)
            {
                if (child.Type != PptTypes.ColorSchemeAtom)
                    continue;

                if (child.Instance == 1)
                    return ReadScheme(child);

                first ??= child;
            }

            return ReadScheme(first);
        }

        private static uint[] ReadScheme(PptRecord atom)
        {
            if (atom == null || atom.Length < SchemeCount * 4)
                return null;

            var scheme = new uint[SchemeCount];
            for (var i = 0; i < SchemeCount; i++)
                scheme[i] = atom.UInt32(i * 4);

            return scheme;
        }

        public static string FromColorIndex(uint value, uint[] scheme)
        {
            var index = (int)(value >> 24);
            if (index == 0xFE)
                return Hex(value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF);

            if (index < SchemeCount && scheme != null)
                return Hex(scheme[index] & 0xFF, (scheme[index] >> 8) & 0xFF, (scheme[index] >> 16) & 0xFF);

            return null;
        }

        public static string FromOfficeArt(uint value, uint[] scheme)
        {
            var flags = (int)(value >> 24);
            var red = value & 0xFF;
            if ((flags & 0x08) != 0)
            {
                if (scheme == null || red >= SchemeCount)
                    return null;

                var color = scheme[red];
                return Hex(color & 0xFF, (color >> 8) & 0xFF, (color >> 16) & 0xFF);
            }

            if ((flags & 0x10) != 0 || (flags & 0x01) != 0)
                return null;

            return Hex(red, (value >> 8) & 0xFF, (value >> 16) & 0xFF);
        }

        private static string Hex(uint red, uint green, uint blue) =>
            ((red << 16) | (green << 8) | blue).ToString("X6", CultureInfo.InvariantCulture);
    }
}
