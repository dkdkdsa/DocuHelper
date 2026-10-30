using System;
using System.IO;
using System.IO.Compression;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpDeflate
    {
        private const int MaxSize = 256 * 1024 * 1024;
        private const int MaxInitialCapacity = 64 * 1024 * 1024;
        private const int BufferSize = 81920;

        public static byte[] Inflate(byte[] data)
        {
            using var input = new MemoryStream(data, false);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream((int)Math.Clamp(data.Length * 4L, 256, MaxInitialCapacity));
            var buffer = new byte[BufferSize];
            int read;
            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MaxSize)
                    throw new InvalidDataException("A stream expands beyond the size limit.");

                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }
    }
}
