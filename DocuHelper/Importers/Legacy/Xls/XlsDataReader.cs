using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsDataReader
    {
        private readonly byte[] _data;
        private readonly int[] _breaks;

        public XlsDataReader(XlsRecord record)
            : this(record.Data, record.Breaks)
        {
        }

        public XlsDataReader(byte[] data, int[] breaks = null)
        {
            _data = data;
            _breaks = breaks ?? Array.Empty<int>();
        }

        public int Position { get; set; }

        public int Length => _data.Length;

        public int Remaining => _data.Length - Position;

        public byte ReadByte()
        {
            Ensure(1);
            return _data[Position++];
        }

        public ushort ReadUInt16()
        {
            Ensure(2);
            var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(Position));
            Position += 2;
            return value;
        }

        public short ReadInt16() => (short)ReadUInt16();

        public uint ReadUInt32()
        {
            Ensure(4);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(Position));
            Position += 4;
            return value;
        }

        public double ReadDouble()
        {
            Ensure(8);
            var value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan(Position)));
            Position += 8;
            return value;
        }

        public byte[] ReadBytes(int count)
        {
            Ensure(count);
            var result = new byte[count];
            Buffer.BlockCopy(_data, Position, result, 0, count);
            Position += count;
            return result;
        }

        public void Skip(int count)
        {
            Ensure(count);
            Position += count;
        }

        public string ReadShortString() => ReadString(ReadByte());

        public string ReadLongString() => ReadString(ReadUInt16());

        public string ReadString(int length)
        {
            var flags = ReadByte();
            var runs = (flags & 0x08) != 0 ? ReadUInt16() : 0;
            var extra = (flags & 0x04) != 0 ? ReadUInt32() : 0;
            var text = ReadChars(length, (flags & 0x01) != 0);
            Skip(runs * 4);
            if (extra > int.MaxValue)
                throw new InvalidDataException("The extended string is too long.");

            Skip((int)extra);
            return text;
        }

        public string ReadChars(int count, bool wide)
        {
            var builder = new StringBuilder(Math.Min(count, Remaining));
            while (count > 0)
            {
                if (Position >= _data.Length)
                    throw new InvalidDataException("The string is cut off.");

                var limit = NextBreak();
                if (Position == limit)
                {
                    wide = (ReadByte() & 1) != 0;
                    continue;
                }

                var unit = wide ? 2 : 1;
                var fit = (limit - Position) / unit;
                if (fit == 0)
                    throw new InvalidDataException("A character is split across records.");

                var take = Math.Min(fit, count);
                if (wide)
                {
                    builder.Append(Encoding.Unicode.GetString(_data, Position, take * 2));
                }
                else
                {
                    for (var i = 0; i < take; i++)
                        builder.Append((char)_data[Position + i]);
                }

                Position += take * unit;
                count -= take;
            }

            return builder.ToString();
        }

        private int NextBreak()
        {
            var index = Array.BinarySearch(_breaks, Position);
            if (index >= 0)
                return _breaks[index];

            index = ~index;
            return index < _breaks.Length ? _breaks[index] : _data.Length;
        }

        private void Ensure(int count)
        {
            if (count < 0 || Position + (long)count > _data.Length)
                throw new InvalidDataException("The record is shorter than expected.");
        }
    }
}
