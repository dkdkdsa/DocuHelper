using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpCompoundFile
    {
        private const uint EndOfChain = 0xFFFFFFFE;
        private const uint NoEntry = 0xFFFFFFFF;
        private const int HeaderSize = 512;
        private const int HeaderDifatCount = 109;
        private const int EntrySize = 128;
        private const int StorageType = 1;
        private const int StreamType = 2;
        private const int RootType = 5;
        private const uint DefaultMiniStreamCutoff = 4096;
        private const int MaxStorageDepth = 8;

        private static readonly byte[] s_signature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

        private readonly byte[] _content;
        private readonly int _sectorSize;
        private readonly int _miniSectorSize;
        private readonly long _sectorCount;
        private readonly uint _miniStreamCutoff;
        private readonly bool _wideSizes;
        private readonly uint[] _fat;
        private readonly uint[] _miniFat;
        private readonly byte[] _miniStream;
        private readonly List<HwpCompoundEntry> _entries = new();
        private readonly Dictionary<string, HwpCompoundEntry> _paths = new(StringComparer.OrdinalIgnoreCase);

        public HwpCompoundFile(byte[] content)
        {
            if (!IsCompoundFile(content) || content.Length < HeaderSize)
                throw new InvalidDataException("The content is not a compound file.");

            _content = content;
            var sectorShift = ReadUInt16(30);
            var miniSectorShift = ReadUInt16(32);
            if (ReadUInt16(28) != 0xFFFE || (sectorShift != 9 && sectorShift != 12) || miniSectorShift < 2 || miniSectorShift >= sectorShift)
                throw new InvalidDataException("The compound file header is broken.");

            _sectorSize = 1 << sectorShift;
            _miniSectorSize = 1 << miniSectorShift;
            _wideSizes = ReadUInt16(26) >= 4;
            _sectorCount = Math.Max(0, (content.Length + (long)_sectorSize - 1) / _sectorSize - 1);
            var cutoff = ReadUInt32(56);
            _miniStreamCutoff = cutoff == 0 ? DefaultMiniStreamCutoff : cutoff;

            _fat = ReadFat(ReadUInt32(44), ReadUInt32(68));
            ReadDirectory(ReadUInt32(48));
            if (_entries.Count == 0 || _entries[0].Type != RootType)
                throw new InvalidDataException("The compound file has no root entry.");

            var root = _entries[0];
            if (root.Size > content.Length)
                throw new InvalidDataException("The mini stream is larger than the file.");

            _miniStream = root.Size > 0 ? ReadSectors(root.StartSector, root.Size) : Array.Empty<byte>();
            _miniFat = ReadTable(ReadUInt32(60), ReadUInt32(64));
            Walk(root.Child);
        }

        public static bool IsCompoundFile(byte[] content)
        {
            if (content == null || content.Length < s_signature.Length)
                return false;

            for (var i = 0; i < s_signature.Length; i++)
            {
                if (content[i] != s_signature[i])
                    return false;
            }

            return true;
        }

        public Guid RootClassId => _entries[0].ClassId;

        public bool HasStorage(string path) => _paths.TryGetValue(path, out var entry) && entry.Type == StorageType;

        public byte[] ReadStream(string path)
        {
            if (!_paths.TryGetValue(path, out var entry) || entry.Type != StreamType)
                return null;

            if (entry.Size <= 0)
                return Array.Empty<byte>();

            if (entry.Size > _content.Length)
                throw new InvalidDataException("The stream is larger than the file.");

            return entry.Size < _miniStreamCutoff ? ReadMiniSectors(entry.StartSector, entry.Size) : ReadSectors(entry.StartSector, entry.Size);
        }

        public List<string> StreamNames(string storagePath)
        {
            var prefix = string.IsNullOrEmpty(storagePath) ? string.Empty : storagePath + "/";
            var names = new List<string>();
            foreach (var pair in _paths)
            {
                if (pair.Value.Type != StreamType || !pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = pair.Key.Substring(prefix.Length);
                if (name.IndexOf('/') < 0)
                    names.Add(name);
            }

            return names;
        }

        private uint[] ReadFat(uint fatSectorCount, uint firstDifatSector)
        {
            var fatSectors = new List<uint>();
            for (var i = 0; i < HeaderDifatCount; i++)
                fatSectors.Add(ReadUInt32(76 + i * 4));

            var perSector = _sectorSize / 4;
            var difat = firstDifatSector;
            var visited = new HashSet<uint>();
            while (fatSectors.Count < fatSectorCount && difat != EndOfChain && difat != NoEntry)
            {
                if (difat >= _sectorCount || !visited.Add(difat))
                    throw new InvalidDataException("The DIFAT chain is broken.");

                var offset = SectorOffset(difat);
                for (var i = 0; i < perSector - 1; i++)
                    fatSectors.Add(ReadUInt32(offset + i * 4));

                difat = ReadUInt32(offset + (perSector - 1) * 4);
            }

            var count = (int)Math.Min(fatSectorCount, (uint)fatSectors.Count);
            var fat = new uint[(long)count * perSector];
            for (var i = 0; i < count; i++)
            {
                var sector = fatSectors[i];
                if (sector >= _sectorCount)
                    throw new InvalidDataException("A FAT sector is out of range.");

                var offset = SectorOffset(sector);
                for (var j = 0; j < perSector; j++)
                    fat[(long)i * perSector + j] = ReadUInt32(offset + j * 4);
            }

            return fat;
        }

        private uint[] ReadTable(uint firstSector, uint sectorCount)
        {
            if (sectorCount == 0 || firstSector == EndOfChain || firstSector == NoEntry)
                return Array.Empty<uint>();

            if (sectorCount > _sectorCount)
                throw new InvalidDataException("The mini FAT is larger than the file.");

            var bytes = ReadSectors(firstSector, (long)sectorCount * _sectorSize);
            var table = new uint[bytes.Length / 4];
            for (var i = 0; i < table.Length; i++)
                table[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4));

            return table;
        }

        private void ReadDirectory(uint firstSector)
        {
            var sector = firstSector;
            var steps = 0L;
            while (sector != EndOfChain && sector != NoEntry)
            {
                if (sector >= _sectorCount || sector >= _fat.Length || steps++ > _fat.Length)
                    throw new InvalidDataException("The directory chain is broken.");

                var offset = SectorOffset(sector);
                for (var i = 0; i + EntrySize <= _sectorSize && offset + i + EntrySize <= _content.Length; i += EntrySize)
                    _entries.Add(ReadEntry((int)(offset + i)));

                sector = _fat[sector];
            }
        }

        private HwpCompoundEntry ReadEntry(int offset)
        {
            var nameBytes = Math.Clamp((int)ReadUInt16(offset + 64), 0, 64);
            var name = Encoding.Unicode.GetString(_content, offset, nameBytes & ~1).TrimEnd('\0');
            var size = _wideSizes ? (long)BinaryPrimitives.ReadUInt64LittleEndian(_content.AsSpan(offset + 120)) : ReadUInt32(offset + 120);
            return new HwpCompoundEntry
            {
                Name = name,
                Type = _content[offset + 66],
                Left = ReadUInt32(offset + 68),
                Right = ReadUInt32(offset + 72),
                Child = ReadUInt32(offset + 76),
                StartSector = ReadUInt32(offset + 116),
                ClassId = new Guid(_content.AsSpan(offset + 80, 16)),
                Size = size < 0 ? long.MaxValue : size,
            };
        }

        private void Walk(uint first)
        {
            var visited = new HashSet<uint> { 0 };
            var pending = new Stack<(uint Index, string Prefix, int Depth)>();
            pending.Push((first, string.Empty, 0));
            while (pending.Count > 0)
            {
                var (index, prefix, depth) = pending.Pop();
                if (index == NoEntry || index >= _entries.Count || !visited.Add(index))
                    continue;

                var entry = _entries[(int)index];
                pending.Push((entry.Right, prefix, depth));
                pending.Push((entry.Left, prefix, depth));
                if (entry.Type != StorageType && entry.Type != StreamType)
                    continue;

                var path = prefix + entry.Name;
                _paths.TryAdd(path, entry);
                if (entry.Type == StorageType && depth < MaxStorageDepth)
                    pending.Push((entry.Child, path + "/", depth + 1));
            }
        }

        private byte[] ReadSectors(uint start, long size)
        {
            var result = new byte[size];
            var sector = start;
            var written = 0L;
            var steps = 0L;
            while (written < size)
            {
                if (sector == EndOfChain)
                    break;

                if (sector >= _sectorCount || sector >= _fat.Length || steps++ > _fat.Length)
                    throw new InvalidDataException("A sector chain is broken.");

                var offset = SectorOffset(sector);
                var count = (int)Math.Min(_sectorSize, size - written);
                var available = (int)Math.Clamp(_content.Length - offset, 0, count);
                if (available > 0)
                    Buffer.BlockCopy(_content, (int)offset, result, (int)written, available);

                written += count;
                sector = _fat[sector];
            }

            if (written < size)
                Array.Resize(ref result, (int)written);

            return result;
        }

        private byte[] ReadMiniSectors(uint start, long size)
        {
            var result = new byte[size];
            var sector = start;
            var written = 0L;
            var steps = 0L;
            while (written < size)
            {
                if (sector == EndOfChain)
                    break;

                if (sector >= _miniFat.Length || steps++ > _miniFat.Length)
                    throw new InvalidDataException("A mini sector chain is broken.");

                var offset = (long)sector * _miniSectorSize;
                var count = (int)Math.Min(_miniSectorSize, size - written);
                var available = (int)Math.Clamp(_miniStream.Length - offset, 0, count);
                if (available > 0)
                    Buffer.BlockCopy(_miniStream, (int)offset, result, (int)written, available);

                written += count;
                sector = _miniFat[sector];
            }

            if (written < size)
                Array.Resize(ref result, (int)written);

            return result;
        }

        private long SectorOffset(uint sector) => (sector + 1L) * _sectorSize;

        private ushort ReadUInt16(long offset) =>
            offset + 2 <= _content.Length ? BinaryPrimitives.ReadUInt16LittleEndian(_content.AsSpan((int)offset)) : (ushort)0;

        private uint ReadUInt32(long offset) =>
            offset + 4 <= _content.Length ? BinaryPrimitives.ReadUInt32LittleEndian(_content.AsSpan((int)offset)) : NoEntry;
    }
}
