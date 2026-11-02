namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocSprmReader
    {
        private const int TableDefinition = 0xD608;
        private const int ChangeTabs = 0xC615;

        private static readonly int[] s_fixedSizes = { 1, 1, 2, 4, 2, 2, -1, 3 };

        public static bool Next(byte[] data, ref int position, int end, out DocSprm sprm)
        {
            sprm = default;
            if (data == null || end > data.Length || position < 0 || position + 2 > end)
                return false;

            var op = data[position] | (data[position + 1] << 8);
            var start = position + 2;
            var size = s_fixedSizes[op >> 13];
            int offset;
            int length;
            if (size >= 0)
            {
                offset = start;
                length = size;
            }
            else if (op == TableDefinition)
            {
                if (start + 2 > end)
                    return false;

                var total = data[start] | (data[start + 1] << 8);
                offset = start + 2;
                length = total < 1 ? 0 : total - 1;
            }
            else
            {
                if (start + 1 > end)
                    return false;

                var count = data[start];
                offset = start + 1;
                length = count;
                if (op == ChangeTabs && count == 255)
                {
                    if (offset >= end)
                        return false;

                    var deleted = data[offset];
                    var addedAt = offset + 1 + deleted * 4;
                    if (addedAt >= end)
                        return false;

                    length = 1 + deleted * 4 + 1 + data[addedAt] * 3;
                }
            }

            if (offset + length > end)
                return false;

            sprm = new DocSprm(op, offset, length);
            position = offset + length;
            return true;
        }
    }
}
