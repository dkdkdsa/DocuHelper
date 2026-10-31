using System;

namespace DocuHelper.Tools.Ppt
{
    public static class PptImageSniffer
    {
        public static PptImageInfo Sniff(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 12)
                return null;

            PptImageInfo info;
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            {
                info = Create(PptImageType.Png, "image/png", ".png");
                if (bytes.Length >= 24)
                {
                    info.WidthPx = ClampSize(ReadUInt32(bytes, 16, true));
                    info.HeightPx = ClampSize(ReadUInt32(bytes, 20, true));
                }
            }
            else if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                info = Create(PptImageType.Jpeg, "image/jpeg", ".jpg");
                ReadJpeg(bytes, info);
            }
            else if (bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == '8' && (bytes[4] == '7' || bytes[4] == '9') && bytes[5] == 'a')
            {
                info = Create(PptImageType.Gif, "image/gif", ".gif");
                info.WidthPx = ReadUInt16(bytes, 6, false);
                info.HeightPx = ReadUInt16(bytes, 8, false);
            }
            else if (bytes[0] == 'B' && bytes[1] == 'M')
            {
                info = Create(PptImageType.Bmp, "image/bmp", ".bmp");
                ReadBmp(bytes, info);
            }
            else if ((bytes[0] == 'I' && bytes[1] == 'I' && bytes[2] == 0x2A && bytes[3] == 0) || (bytes[0] == 'M' && bytes[1] == 'M' && bytes[2] == 0 && bytes[3] == 0x2A))
            {
                info = Create(PptImageType.Tiff, "image/tiff", ".tif");
                ReadTiff(bytes, info);
            }
            else
            {
                return null;
            }

            info.ByteCount = bytes.Length;
            return info;
        }

        private static PptImageInfo Create(PptImageType type, string mime, string extension) =>
            new() { Type = type, Mime = mime, Extension = extension };

        private static void ReadJpeg(byte[] bytes, PptImageInfo info)
        {
            var position = 2;
            while (position + 4 <= bytes.Length)
            {
                if (bytes[position] != 0xFF)
                {
                    position++;
                    continue;
                }

                var marker = bytes[position + 1];
                if (marker == 0xFF)
                {
                    position++;
                    continue;
                }

                if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD9))
                {
                    position += 2;
                    continue;
                }

                var length = ReadUInt16(bytes, position + 2, true);
                var isFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (isFrame && position + 9 <= bytes.Length)
                {
                    info.HeightPx = ReadUInt16(bytes, position + 5, true);
                    info.WidthPx = ReadUInt16(bytes, position + 7, true);
                    return;
                }

                if (length < 2)
                    return;

                position += 2 + length;
            }
        }

        private static void ReadBmp(byte[] bytes, PptImageInfo info)
        {
            if (bytes.Length < 26)
                return;

            var headerSize = ReadUInt32(bytes, 14, false);
            if (headerSize == 12)
            {
                info.WidthPx = ReadUInt16(bytes, 18, false);
                info.HeightPx = ReadUInt16(bytes, 20, false);
                return;
            }

            if (headerSize < 40)
                return;

            var width = (int)ReadUInt32(bytes, 18, false);
            var height = (int)ReadUInt32(bytes, 22, false);
            info.WidthPx = ClampSize(Math.Abs((long)width));
            info.HeightPx = ClampSize(Math.Abs((long)height));
        }

        private static void ReadTiff(byte[] bytes, PptImageInfo info)
        {
            var big = bytes[0] == 'M';
            var offset = ReadUInt32(bytes, 4, big);
            if (offset < 8 || offset + 2 > bytes.Length)
                return;

            var count = ReadUInt16(bytes, (int)offset, big);
            for (var i = 0; i < count; i++)
            {
                var entry = (int)offset + 2 + i * 12;
                if (entry + 12 > bytes.Length)
                    return;

                var tag = ReadUInt16(bytes, entry, big);
                if (tag != 256 && tag != 257)
                    continue;

                var kind = ReadUInt16(bytes, entry + 2, big);
                var value = kind == 3 ? ReadUInt16(bytes, entry + 8, big) : ClampSize(ReadUInt32(bytes, entry + 8, big));
                if (tag == 256)
                    info.WidthPx = value;
                else
                    info.HeightPx = value;
            }
        }

        private static int ReadUInt16(byte[] bytes, int position, bool bigEndian) =>
            bigEndian ? (bytes[position] << 8) | bytes[position + 1] : (bytes[position + 1] << 8) | bytes[position];

        private static long ReadUInt32(byte[] bytes, int position, bool bigEndian)
        {
            if (bigEndian)
                return ((long)bytes[position] << 24) | ((long)bytes[position + 1] << 16) | ((long)bytes[position + 2] << 8) | bytes[position + 3];

            return ((long)bytes[position + 3] << 24) | ((long)bytes[position + 2] << 16) | ((long)bytes[position + 1] << 8) | bytes[position];
        }

        private static int ClampSize(long value) => value < 0 || value > int.MaxValue ? 0 : (int)value;
    }
}
