using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace DocuHelperEditor.Services
{
    public static class MetafileImages
    {
        private const int MaxSide = 2000;
        private const double Scale = 2.0;
        private const uint KeyColor = 0x00FF00FF;
        private const uint EmfSignature = 0x464D4520;

        public static bool IsMetafile(byte[] bytes) => IsEmf(bytes) || IsWmf(bytes);

        public static string ToPngDataUri(byte[] bytes)
        {
            var png = ToPng(bytes);
            return png == null ? null : "data:image/png;base64," + Convert.ToBase64String(png);
        }

        public static byte[] ToPng(byte[] bytes)
        {
            if (!OperatingSystem.IsWindows() || bytes == null || !IsMetafile(bytes))
                return null;

            try
            {
                return Render(bytes);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or ExternalException or ArgumentException or OverflowException)
            {
                return null;
            }
        }

        private static bool IsEmf(byte[] bytes) =>
            bytes.Length >= 88 && BinaryPrimitives.ReadUInt32LittleEndian(bytes) == 1 && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(40)) == EmfSignature;

        private static bool IsWmf(byte[] bytes) =>
            bytes.Length >= 22 && (BinaryPrimitives.ReadUInt32LittleEndian(bytes) == 0x9AC6CDD7 || BinaryPrimitives.ReadUInt16LittleEndian(bytes) is 1 or 2 && BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)) == 9);

        private static byte[] Render(byte[] bytes)
        {
            var handle = IntPtr.Zero;
            var width = 0;
            var height = 0;
            if (IsEmf(bytes))
            {
                handle = SetEnhMetaFileBits((uint)bytes.Length, bytes);
                var left = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24));
                var top = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28));
                var right = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(32));
                var bottom = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(36));
                width = (int)Math.Round((right - left) / 2540.0 * 96 * Scale);
                height = (int)Math.Round((bottom - top) / 2540.0 * 96 * Scale);
            }
            else
            {
                var placeable = BinaryPrimitives.ReadUInt32LittleEndian(bytes) == 0x9AC6CDD7;
                var body = placeable ? bytes.AsSpan(22).ToArray() : bytes;
                if (placeable)
                {
                    var unitsPerInch = Math.Max((int)BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(14)), 1);
                    var w = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(10)) - BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6));
                    var h = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(12)) - BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(8));
                    width = (int)Math.Round(Math.Abs(w) * 96.0 * Scale / unitsPerInch);
                    height = (int)Math.Round(Math.Abs(h) * 96.0 * Scale / unitsPerInch);
                }

                handle = SetWinMetaFileBits((uint)body.Length, body, IntPtr.Zero, IntPtr.Zero);
            }

            if (handle == IntPtr.Zero)
                return null;

            try
            {
                if (width <= 0 || height <= 0)
                {
                    width = 400;
                    height = 300;
                }

                var fit = Math.Min(1.0, MaxSide / (double)Math.Max(width, height));
                width = Math.Max(1, (int)(width * fit));
                height = Math.Max(1, (int)(height * fit));
                return Draw(handle, width, height);
            }
            finally
            {
                DeleteEnhMetaFile(handle);
            }
        }

        private static byte[] Draw(IntPtr metafile, int width, int height)
        {
            var screen = GetDC(IntPtr.Zero);
            var dc = CreateCompatibleDC(screen);
            ReleaseDC(IntPtr.Zero, screen);
            var info = new BitmapInfo
            {
                Size = (uint)Marshal.SizeOf<BitmapInfo>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
            };
            var bitmap = CreateDIBSection(dc, ref info, 0, out var bits, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero)
            {
                DeleteDC(dc);
                return null;
            }

            var old = SelectObject(dc, bitmap);
            try
            {
                var pixels = new int[width * height];
                Array.Fill(pixels, unchecked((int)KeyColor));
                Marshal.Copy(pixels, 0, bits, pixels.Length);
                var rect = new Rect { Right = width, Bottom = height };
                PlayEnhMetaFile(dc, metafile, ref rect);
                GdiFlush();
                Marshal.Copy(bits, pixels, 0, pixels.Length);
                return Png(pixels, width, height);
            }
            finally
            {
                SelectObject(dc, old);
                DeleteObject(bitmap);
                DeleteDC(dc);
            }
        }

        private static byte[] Png(int[] pixels, int width, int height)
        {
            using var raw = new MemoryStream();
            using (var zlib = new ZLibStream(raw, CompressionLevel.Fastest, true))
            {
                var row = new byte[1 + width * 4];
                for (var y = 0; y < height; y++)
                {
                    row[0] = 0;
                    for (var x = 0; x < width; x++)
                    {
                        var value = (uint)pixels[y * width + x] & 0x00FFFFFF;
                        var at = 1 + x * 4;
                        row[at] = (byte)(value >> 16);
                        row[at + 1] = (byte)(value >> 8);
                        row[at + 2] = (byte)value;
                        row[at + 3] = value == KeyColor ? (byte)0 : (byte)255;
                    }

                    zlib.Write(row, 0, row.Length);
                }
            }

            using var png = new MemoryStream();
            png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header, width);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
            header[8] = 8;
            header[9] = 6;
            Chunk(png, "IHDR", header);
            Chunk(png, "IDAT", raw.ToArray());
            Chunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        private static void Chunk(Stream stream, string type, byte[] data)
        {
            var length = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            stream.Write(length);
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes);
            stream.Write(data);
            var crc = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(typeBytes, data));
            stream.Write(crc);
        }

        private static uint Crc(byte[] type, byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in type)
                crc = Step(crc, b);

            foreach (var b in data)
                crc = Step(crc, b);

            return crc ^ 0xFFFFFFFFu;
        }

        private static uint Step(uint crc, byte value)
        {
            crc ^= value;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;

            return crc;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ClrUsed;
            public uint ClrImportant;
            public uint Colors;
        }

        [DllImport("gdi32.dll")]
        private static extern IntPtr SetEnhMetaFileBits(uint size, byte[] data);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SetWinMetaFileBits(uint size, byte[] data, IntPtr reference, IntPtr picture);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteEnhMetaFile(IntPtr metafile);

        [DllImport("gdi32.dll")]
        private static extern bool PlayEnhMetaFile(IntPtr dc, IntPtr metafile, ref Rect bounds);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr item);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr item);

        [DllImport("gdi32.dll")]
        private static extern bool GdiFlush();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    }
}
