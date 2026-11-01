using System;
using System.IO;
using System.IO.Compression;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocPictureReader
    {
        private const int HeaderSize = 0x44;
        private const int NamedShapeFile = 0x66;
        private const int ShapeContainer = 0xF004;
        private const int BlipStoreEntry = 0xF007;
        private const int FirstBlip = 0xF018;
        private const int LastBlip = 0xF117;
        private const int EmuPerTwip = 635;
        private const int DefaultTwips = 3000;
        private const int MetafileHeader = 34;
        private const int MaxInflated = 64 << 20;
        private const int MaxRecords = 64;

        public static DocPicture Read(byte[] data, long offset)
        {
            if (!DocBytes.Has(data, offset, HeaderSize))
                return null;

            var total = DocBytes.I32(data, offset);
            var header = DocBytes.U16(data, offset + 4);
            if (total < header || header < HeaderSize)
                return null;

            var end = Math.Min(offset + total, (long)data.Length);
            var width = DocBytes.I16(data, offset + 28) * (long)DocBytes.U16(data, offset + 32) / 1000;
            var height = DocBytes.I16(data, offset + 30) * (long)DocBytes.U16(data, offset + 34) / 1000;
            var position = offset + header;
            if (DocBytes.U16(data, offset + 6) == NamedShapeFile && position < end)
                position += 1 + DocBytes.U8(data, position);

            for (var count = 0; count < MaxRecords && position + 8 <= end; count++)
            {
                var type = DocBytes.U16(data, position + 2);
                var length = DocBytes.U32(data, position + 4);
                var body = position + 8;
                if (length > end - body)
                    length = end - body;

                DocPicture picture = null;
                if (type == BlipStoreEntry)
                    picture = ReadStoreEntry(data, body, length);
                else if (type >= FirstBlip && type <= LastBlip)
                    picture = ReadBlip(data, position);

                if (picture != null)
                {
                    picture.WidthEmu = (width > 0 ? width : DefaultTwips) * EmuPerTwip;
                    picture.HeightEmu = (height > 0 ? height : DefaultTwips) * EmuPerTwip;
                    return picture;
                }

                if (type != ShapeContainer && type != BlipStoreEntry && (type < FirstBlip || type > LastBlip))
                    break;

                position = body + length;
            }

            return null;
        }

        private static DocPicture ReadStoreEntry(byte[] data, long body, long length)
        {
            if (length < 36 || !DocBytes.Has(data, body, length))
                return null;

            var nameLength = DocBytes.U8(data, body + 33);
            var blip = body + 36 + nameLength;
            return blip + 8 <= body + length ? ReadBlip(data, blip) : null;
        }

        private static DocPicture ReadBlip(byte[] data, long position)
        {
            var info = DocBytes.U16(data, position);
            var type = DocBytes.U16(data, position + 2);
            var length = DocBytes.U32(data, position + 4);
            var body = position + 8;
            if (length > data.Length - body)
                length = data.Length - body;

            var cursor = body + (((info >> 4) & 1) == 1 ? 32 : 16);
            var end = body + length;
            switch (type)
            {
                case 0xF01D:
                case 0xF02A:
                    return Bitmap(data, cursor + 1, end, "image/jpeg");
                case 0xF01E:
                    return Bitmap(data, cursor + 1, end, "image/png");
                case 0xF029:
                    return Bitmap(data, cursor + 1, end, "image/tiff");
                case 0xF01A:
                    return Metafile(data, cursor, end, "image/x-emf");
                case 0xF01B:
                    return Metafile(data, cursor, end, "image/x-wmf");
                default:
                    return null;
            }
        }

        private static DocPicture Bitmap(byte[] data, long start, long end, string type)
        {
            if (start >= end || !DocBytes.Has(data, start, end - start))
                return null;

            return new DocPicture { Bytes = DocBytes.Slice(data, start, end - start), ContentType = type };
        }

        private static DocPicture Metafile(byte[] data, long start, long end, string type)
        {
            if (start + MetafileHeader > end)
                return null;

            var saved = DocBytes.U32(data, start + 28);
            var compression = DocBytes.U8(data, start + 32);
            var body = start + MetafileHeader;
            var size = Math.Min(saved, end - body);
            if (size <= 0)
                return null;

            var raw = DocBytes.Slice(data, body, size);
            if (compression != 0)
                return new DocPicture { Bytes = raw, ContentType = type };

            try
            {
                using var source = new MemoryStream(raw);
                using var inflater = new ZLibStream(source, CompressionMode.Decompress);
                using var target = new MemoryStream();
                var buffer = new byte[16384];
                int read;
                while ((read = inflater.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (target.Length + read > MaxInflated)
                        return null;

                    target.Write(buffer, 0, read);
                }

                return new DocPicture { Bytes = target.ToArray(), ContentType = type };
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }
    }
}
