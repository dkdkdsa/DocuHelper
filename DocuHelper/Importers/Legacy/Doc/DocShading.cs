namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocShading
    {
        private static readonly int[] s_percent = { 0, 100, 5, 10, 20, 25, 30, 40, 50, 60, 70, 75, 80, 90 };

        public static int FromShd80(byte[] data, int offset)
        {
            var value = data[offset] | (data[offset + 1] << 8);
            return Blend(DocColors.FromIndex(value & 0x1F), DocColors.FromIndex((value >> 5) & 0x1F), (value >> 10) & 0x3F);
        }

        public static int FromShd(byte[] data, int offset)
        {
            var fore = data[offset + 3] == 0xFF ? -1 : (data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2];
            var back = data[offset + 7] == 0xFF ? -1 : (data[offset + 4] << 16) | (data[offset + 5] << 8) | data[offset + 6];
            return Blend(fore, back, data[offset + 8] | (data[offset + 9] << 8));
        }

        private static int Blend(int fore, int back, int pattern)
        {
            if (pattern == 0)
                return back;

            if (pattern == 0x3F || pattern == 1)
                return fore < 0 ? 0 : fore;

            var percent = pattern < s_percent.Length ? s_percent[pattern] : 50;
            var f = fore < 0 ? 0 : fore;
            var b = back < 0 ? 0xFFFFFF : back;
            var result = 0;
            for (var shift = 16; shift >= 0; shift -= 8)
            {
                var fc = (f >> shift) & 0xFF;
                var bc = (b >> shift) & 0xFF;
                result |= ((fc * percent + bc * (100 - percent)) / 100) << shift;
            }

            return result;
        }
    }
}
