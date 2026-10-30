using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpRecord
    {
        private const int ExtendedSize = 0xFFF;

        private readonly byte[] _buffer;
        private readonly int _offset;

        public HwpRecord(int tag, int level, byte[] buffer, int offset, int size)
        {
            Tag = tag;
            Level = level;
            _buffer = buffer;
            _offset = offset;
            Size = size;
        }

        public int Tag { get; }

        public int Level { get; }

        public int Size { get; }

        public List<HwpRecord> Children { get; } = new();

        public string Id => IdOf(UInt32(0));

        public static List<HwpRecord> ReadList(byte[] data)
        {
            var records = new List<HwpRecord>();
            var position = 0;
            while (data.Length - position >= 4)
            {
                var header = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position));
                position += 4;
                var size = (long)(header >> 20);
                if (size == ExtendedSize)
                {
                    if (data.Length - position < 4)
                        throw new InvalidDataException("A record header is cut off.");

                    size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position));
                    position += 4;
                }

                if (size > data.Length - position)
                    throw new InvalidDataException("A record is cut off.");

                records.Add(new HwpRecord((int)(header & 0x3FF), (int)((header >> 10) & 0x3FF), data, position, (int)size));
                position += (int)size;
            }

            return records;
        }

        public static List<HwpRecord> ReadTree(byte[] data)
        {
            var roots = new List<HwpRecord>();
            var open = new List<HwpRecord>();
            foreach (var record in ReadList(data))
            {
                while (open.Count > 0 && open[^1].Level >= record.Level)
                    open.RemoveAt(open.Count - 1);

                if (open.Count == 0)
                    roots.Add(record);
                else
                    open[^1].Children.Add(record);

                open.Add(record);
            }

            return roots;
        }

        public bool Has(int at, int count) => at >= 0 && count >= 0 && at <= Size - count;

        public byte Byte(int at) => Has(at, 1) ? _buffer[_offset + at] : (byte)0;

        public ushort UInt16(int at) => Has(at, 2) ? BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(_offset + at)) : (ushort)0;

        public uint UInt32(int at) => Has(at, 4) ? BinaryPrimitives.ReadUInt32LittleEndian(_buffer.AsSpan(_offset + at)) : 0U;

        public int Int32(int at) => (int)UInt32(at);

        public string Chars(int at, int count)
        {
            var available = Math.Min(count, (Size - at) / 2);
            return available > 0 && at >= 0 ? Encoding.Unicode.GetString(_buffer, _offset + at, available * 2) : string.Empty;
        }

        public string HwpString(int at, out int next)
        {
            var length = UInt16(at);
            next = at + 2 + length * 2;
            return Chars(at + 2, length);
        }

        private static string IdOf(uint value) =>
            new string(new[] { (char)((value >> 24) & 0xFF), (char)((value >> 16) & 0xFF), (char)((value >> 8) & 0xFF), (char)(value & 0xFF) });
    }
}
