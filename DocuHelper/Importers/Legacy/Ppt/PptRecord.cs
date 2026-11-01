using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptRecord
    {
        public const int HeaderSize = 8;

        private const int MaxChildren = 200000;

        private readonly byte[] _buffer;
        private List<PptRecord> _children;

        private PptRecord(byte[] buffer, int start, int version, int instance, int type, int length)
        {
            _buffer = buffer;
            Start = start;
            Version = version;
            Instance = instance;
            Type = type;
            Length = length;
        }

        public int Start { get; }

        public int Version { get; }

        public int Instance { get; }

        public int Type { get; }

        public int Length { get; }

        public int DataStart => Start + HeaderSize;

        public int End => DataStart + Length;

        public bool IsContainer => Version == 0xF;

        public IReadOnlyList<PptRecord> Children
        {
            get
            {
                if (_children != null)
                    return _children;

                var list = new List<PptRecord>();
                if (IsContainer)
                {
                    var position = DataStart;
                    while (list.Count < MaxChildren)
                    {
                        var child = Parse(_buffer, position, End);
                        if (child == null)
                            break;

                        list.Add(child);
                        position = child.End;
                    }
                }

                _children = list;
                return _children;
            }
        }

        public static PptRecord Parse(byte[] buffer, int position, int limit)
        {
            if (buffer == null || position < 0 || limit > buffer.Length || limit - position < HeaderSize)
                return null;

            var first = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(position));
            var type = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(position + 2));
            var length = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(position + 4));
            if (length > (uint)(limit - position - HeaderSize))
                return null;

            return new PptRecord(buffer, position, first & 0xF, first >> 4, type, (int)length);
        }

        public PptRecord Find(int type)
        {
            foreach (var child in Children)
            {
                if (child.Type == type)
                    return child;
            }

            return null;
        }

        public bool Has(int at, int count) => at >= 0 && count >= 0 && at <= Length - count;

        public byte Byte(int at) => Has(at, 1) ? _buffer[DataStart + at] : (byte)0;

        public ushort UInt16(int at) => Has(at, 2) ? BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(DataStart + at)) : (ushort)0;

        public short Int16(int at) => (short)UInt16(at);

        public uint UInt32(int at) => Has(at, 4) ? BinaryPrimitives.ReadUInt32LittleEndian(_buffer.AsSpan(DataStart + at)) : 0U;

        public int Int32(int at) => (int)UInt32(at);

        public byte[] Bytes(int at, int count)
        {
            if (!Has(at, count))
                return Array.Empty<byte>();

            var result = new byte[count];
            Buffer.BlockCopy(_buffer, DataStart + at, result, 0, count);
            return result;
        }

        public string Utf16(int at, int count)
        {
            var available = Math.Min(count, (Length - at) / 2);
            return available > 0 && at >= 0 ? Encoding.Unicode.GetString(_buffer, DataStart + at, available * 2) : string.Empty;
        }

        public string Latin1(int at, int count)
        {
            var available = Math.Min(count, Length - at);
            return available > 0 && at >= 0 ? Encoding.Latin1.GetString(_buffer, DataStart + at, available) : string.Empty;
        }
    }
}
