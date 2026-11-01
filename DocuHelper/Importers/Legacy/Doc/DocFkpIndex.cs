using System;
using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocFkpIndex
    {
        private const int PageSize = 512;
        private const int MaxPlcEntries = 1 << 22;
        private const int HugeParagraph = 0x6646;
        private const int TableProperties = 0x646B;
        private const int MaxHuge = 1 << 20;

        private static readonly byte[] s_empty = Array.Empty<byte>();

        private readonly List<DocFkpEntry> _entries = new();

        public static DocFkpIndex ReadCharacters(byte[] word, byte[] table, DocRange plc)
        {
            var index = new DocFkpIndex();
            foreach (var page in Pages(word, table, plc))
            {
                var count = word[page + PageSize - 1];
                if ((count + 1) * 4 + count > PageSize - 1)
                    continue;

                for (var i = 0; i < count; i++)
                {
                    var start = DocBytes.U32(word, page + i * 4);
                    var end = DocBytes.U32(word, page + (i + 1) * 4);
                    var offset = word[page + (count + 1) * 4 + i] * 2;
                    if (offset == 0 || start > int.MaxValue || end > int.MaxValue || end <= start)
                        continue;

                    var size = word[page + offset];
                    if (offset + 1 + size > PageSize)
                        continue;

                    index._entries.Add(new DocFkpEntry { Start = (int)start, End = (int)end, Grpprl = DocBytes.Slice(word, page + offset + 1, size) });
                }
            }

            index.Sort();
            return index;
        }

        public static DocFkpIndex ReadParagraphs(byte[] word, byte[] table, byte[] data, DocRange plc)
        {
            var index = new DocFkpIndex();
            foreach (var page in Pages(word, table, plc))
            {
                var count = word[page + PageSize - 1];
                if ((count + 1) * 4 + count * 13 > PageSize - 1)
                    continue;

                for (var i = 0; i < count; i++)
                {
                    var start = DocBytes.U32(word, page + i * 4);
                    var end = DocBytes.U32(word, page + (i + 1) * 4);
                    if (start > int.MaxValue || end > int.MaxValue || end <= start)
                        continue;

                    var entry = new DocFkpEntry { Start = (int)start, End = (int)end, Grpprl = s_empty };
                    var offset = word[page + (count + 1) * 4 + i * 13] * 2;
                    if (offset != 0)
                        ReadParagraph(word, data, page, offset, entry);

                    index._entries.Add(entry);
                }
            }

            index.Sort();
            return index;
        }

        public DocFkpEntry Find(int fc, ref int hint)
        {
            if (hint >= 0 && hint < _entries.Count && fc >= _entries[hint].Start && fc < _entries[hint].End)
                return _entries[hint];

            var low = 0;
            var high = _entries.Count - 1;
            while (low <= high)
            {
                var middle = (low + high) >> 1;
                var entry = _entries[middle];
                if (fc < entry.Start)
                    high = middle - 1;
                else if (fc >= entry.End)
                    low = middle + 1;
                else
                {
                    hint = middle;
                    return entry;
                }
            }

            return null;
        }

        private static void ReadParagraph(byte[] word, byte[] data, int page, int offset, DocFkpEntry entry)
        {
            int size = word[page + offset];
            var body = page + offset + 1;
            if (size == 0)
            {
                if (offset + 1 >= PageSize)
                    return;

                size = word[page + offset + 1] * 2;
                body = page + offset + 2;
            }
            else
            {
                size = size * 2 - 1;
            }

            if (size < 2 || body - page + size > PageSize)
                return;

            entry.Istd = DocBytes.U16(word, body);
            var grpprl = DocBytes.Slice(word, body + 2, size - 2);
            var position = 0;
            while (DocSprmReader.Next(grpprl, ref position, grpprl.Length, out var sprm))
            {
                if ((sprm.Op != HugeParagraph && sprm.Op != TableProperties) || sprm.Length < 4 || data == null)
                    continue;

                var at = DocBytes.U32(grpprl, sprm.Offset);
                if (!DocBytes.Has(data, at, 2))
                    break;

                var length = DocBytes.U16(data, at);
                if (length > MaxHuge || !DocBytes.Has(data, at + 2, length))
                    break;

                var extra = DocBytes.Slice(data, at + 2, length);
                if (sprm.Op == HugeParagraph)
                {
                    grpprl = extra;
                }
                else
                {
                    var merged = new byte[grpprl.Length + extra.Length];
                    Buffer.BlockCopy(grpprl, 0, merged, 0, grpprl.Length);
                    Buffer.BlockCopy(extra, 0, merged, grpprl.Length, extra.Length);
                    grpprl = merged;
                }

                break;
            }

            entry.Grpprl = grpprl;
        }

        private static IEnumerable<int> Pages(byte[] word, byte[] table, DocRange plc)
        {
            if (plc.IsEmpty || !DocBytes.Has(table, plc.Offset, plc.Length))
                yield break;

            var count = (plc.Length - 4) / 8;
            if (count <= 0 || count > MaxPlcEntries)
                yield break;

            var numbers = plc.Offset + (count + 1) * 4;
            for (var i = 0; i < count; i++)
            {
                var page = (DocBytes.U32(table, numbers + i * 4) & 0x3FFFFF) * PageSize;
                if (page + PageSize <= word.Length)
                    yield return (int)page;
            }
        }

        private void Sort() => _entries.Sort((a, b) => a.Start.CompareTo(b.Start));
    }
}
