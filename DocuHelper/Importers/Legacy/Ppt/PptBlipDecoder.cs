using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal static class PptBlipDecoder
    {
        private const int MetafileHeaderSize = 34;
        private const int MaxImageBytes = 64 * 1024 * 1024;

        public static PptImage Decode(PptRecord blip)
        {
            if (blip == null || blip.IsContainer)
                return null;

            var uidSize = (blip.Instance & 1) != 0 ? 32 : 16;
            switch (blip.Type)
            {
                case 0xF01D:
                    return Bitmap(blip, uidSize, "image/jpeg", IsJpeg);
                case 0xF01E:
                    return Bitmap(blip, uidSize, "image/png", IsPng);
                case 0xF029:
                    return Bitmap(blip, uidSize, "image/tiff", IsTiff);
                case 0xF01F:
                    return Dib(blip, uidSize);
                case 0xF01A:
                    return Metafile(blip, uidSize, "image/x-emf");
                case 0xF01B:
                    return Metafile(blip, uidSize, "image/x-wmf");
                default:
                    return null;
            }
        }

        private static PptImage Bitmap(PptRecord blip, int uidSize, string mime, Func<byte[], bool> check)
        {
            var start = uidSize + 1;
            if (blip.Length <= start)
                return null;

            var bytes = blip.Bytes(start, blip.Length - start);
            return check(bytes) ? new PptImage(bytes, mime) : null;
        }

        private static PptImage Dib(PptRecord blip, int uidSize)
        {
            var start = uidSize + 1;
            if (blip.Length < start + 40)
                return null;

            var dib = blip.Bytes(start, blip.Length - start);
            var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(dib);
            if (headerSize < 12 || headerSize > 124)
                return null;

            var bits = BinaryPrimitives.ReadUInt16LittleEndian(dib.AsSpan(14));
            var used = (int)BinaryPrimitives.ReadUInt32LittleEndian(dib.AsSpan(32));
            var palette = bits <= 8 ? (used == 0 ? 1 << bits : used) * 4 : 0;
            var offset = 14 + (int)headerSize + palette;
            var file = new byte[14 + dib.Length];
            file[0] = (byte)'B';
            file[1] = (byte)'M';
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(2), (uint)file.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(10), (uint)Math.Min(offset, file.Length));
            Buffer.BlockCopy(dib, 0, file, 14, dib.Length);
            return new PptImage(file, "image/bmp");
        }

        private static PptImage Metafile(PptRecord blip, int uidSize, string mime)
        {
            var start = uidSize;
            if (blip.Length < start + MetafileHeaderSize)
                return null;

            var size = blip.UInt32(start);
            var saved = blip.UInt32(start + 28);
            var compression = blip.Byte(start + 32);
            var dataStart = start + MetafileHeaderSize;
            var available = blip.Length - dataStart;
            if (saved == 0 || saved > available)
                saved = (uint)available;

            var data = blip.Bytes(dataStart, (int)saved);
            if (compression == 0xFE)
                return data.Length > 0 ? new PptImage(data, mime) : null;

            if (compression != 0 || size == 0 || size > MaxImageBytes)
                return null;

            try
            {
                using var input = new MemoryStream(data);
                using var inflate = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                var buffer = new byte[81920];
                int read;
                while ((read = inflate.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    if (output.Length > MaxImageBytes)
                        return null;
                }

                return output.Length > 0 ? new PptImage(output.ToArray(), mime) : null;
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        private static bool IsJpeg(byte[] bytes) => bytes.Length > 4 && bytes[0] == 0xFF && bytes[1] == 0xD8;

        private static bool IsPng(byte[] bytes) => bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

        private static bool IsTiff(byte[] bytes) => bytes.Length > 8 && ((bytes[0] == 'I' && bytes[1] == 'I') || (bytes[0] == 'M' && bytes[1] == 'M'));
    }
}
