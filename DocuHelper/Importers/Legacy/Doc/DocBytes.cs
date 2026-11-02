using System;
using System.IO;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocBytes
    {
        public static bool Has(byte[] data, long offset, long count) =>
            data != null && offset >= 0 && count >= 0 && offset + count <= data.Length;

        public static int U8(byte[] data, long offset)
        {
            if (!Has(data, offset, 1))
                throw Broken();

            return data[offset];
        }

        public static int U16(byte[] data, long offset)
        {
            if (!Has(data, offset, 2))
                throw Broken();

            return data[offset] | (data[offset + 1] << 8);
        }

        public static int I16(byte[] data, long offset) => (short)U16(data, offset);

        public static long U32(byte[] data, long offset)
        {
            if (!Has(data, offset, 4))
                throw Broken();

            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        public static int I32(byte[] data, long offset) => unchecked((int)U32(data, offset));

        public static byte[] Slice(byte[] data, long offset, long count)
        {
            if (!Has(data, offset, count))
                throw Broken();

            var result = new byte[count];
            Buffer.BlockCopy(data, (int)offset, result, 0, (int)count);
            return result;
        }

        public static string Utf16(byte[] data, long offset, int chars)
        {
            if (chars < 0 || !Has(data, offset, (long)chars * 2))
                throw Broken();

            return System.Text.Encoding.Unicode.GetString(data, (int)offset, chars * 2);
        }

        public static Exception Broken() => new InvalidDataException("The Word file is broken.");
    }
}
