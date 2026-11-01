using System;
using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocPieceTable
    {
        private const int MaxPieces = 1 << 22;

        private static readonly char[] s_windows1252 =
        {
            '€', '\u0081', '‚', 'ƒ', '„', '…', '†', '‡',
            'ˆ', '‰', 'Š', '‹', 'Œ', '\u008D', 'Ž', '\u008F',
            '\u0090', '‘', '’', '“', '”', '•', '–', '—',
            '˜', '™', 'š', '›', 'œ', '\u009D', 'ž', 'Ÿ',
        };

        private readonly List<DocPiece> _pieces = new();

        public int Length { get; private set; }

        public IReadOnlyList<DocPiece> Pieces => _pieces;

        public static DocPieceTable Read(byte[] table, DocRange clx, int ccpText, long wordLength)
        {
            var result = new DocPieceTable();
            if (clx.IsEmpty || !DocBytes.Has(table, clx.Offset, clx.Length))
                throw DocBytes.Broken();

            var modifiers = new List<byte[]>();
            var position = clx.Offset;
            var end = clx.Offset + clx.Length;
            while (position < end)
            {
                var kind = DocBytes.U8(table, position);
                if (kind == 1)
                {
                    var size = DocBytes.U16(table, position + 1);
                    modifiers.Add(DocBytes.Slice(table, position + 3, size));
                    position += 3 + size;
                }
                else if (kind == 2)
                {
                    var size = DocBytes.U32(table, position + 1);
                    result.ReadPlc(table, position + 5, size, modifiers);
                    break;
                }
                else
                {
                    throw DocBytes.Broken();
                }
            }

            if (result._pieces.Count == 0)
                throw DocBytes.Broken();

            result.Length = Math.Min(ccpText, result._pieces[^1].CpEnd);
            foreach (var piece in result._pieces)
            {
                if (piece.CpStart >= result.Length)
                    break;

                if (piece.ByteOf(Math.Min(piece.CpEnd, result.Length)) > wordLength)
                    throw DocBytes.Broken();
            }

            return result;
        }

        public int FindIndex(int cp, int hint)
        {
            if (hint >= 0 && hint < _pieces.Count && cp >= _pieces[hint].CpStart && cp < _pieces[hint].CpEnd)
                return hint;

            var low = 0;
            var high = _pieces.Count - 1;
            while (low <= high)
            {
                var middle = (low + high) >> 1;
                var piece = _pieces[middle];
                if (cp < piece.CpStart)
                    high = middle - 1;
                else if (cp >= piece.CpEnd)
                    low = middle + 1;
                else
                    return middle;
            }

            return -1;
        }

        public string Decode(byte[] word)
        {
            var text = new char[Length];
            foreach (var piece in _pieces)
            {
                var stop = Math.Min(piece.CpEnd, Length);
                for (var cp = piece.CpStart; cp < stop; cp++)
                {
                    var offset = piece.ByteOf(cp);
                    if (piece.Compressed)
                    {
                        text[cp] = DocBytes.Has(word, offset, 1) ? Map(word[offset]) : '\0';
                    }
                    else
                    {
                        text[cp] = DocBytes.Has(word, offset, 2) ? (char)(word[offset] | (word[offset + 1] << 8)) : '\0';
                    }
                }
            }

            return new string(text);
        }

        private static char Map(byte value) => value >= 0x80 && value <= 0x9F ? s_windows1252[value - 0x80] : (char)value;

        private void ReadPlc(byte[] table, long offset, long size, List<byte[]> modifiers)
        {
            if (size < 4 || !DocBytes.Has(table, offset, size))
                throw DocBytes.Broken();

            var count = (size - 4) / 12;
            if (count <= 0 || count > MaxPieces)
                throw DocBytes.Broken();

            var data = offset + (count + 1) * 4;
            var previous = 0;
            for (var i = 0; i < count; i++)
            {
                var start = DocBytes.I32(table, offset + i * 4);
                var stop = DocBytes.I32(table, offset + (i + 1) * 4);
                if (start < previous || stop < start)
                    throw DocBytes.Broken();

                previous = stop;
                var pcd = data + i * 8;
                var fc = DocBytes.U32(table, pcd + 2);
                var prm = DocBytes.U16(table, pcd + 6);
                var compressed = (fc & 0x40000000) != 0;
                fc &= 0x3FFFFFFF;
                var piece = new DocPiece
                {
                    CpStart = start,
                    CpEnd = stop,
                    Compressed = compressed,
                    ByteStart = compressed ? fc / 2 : fc,
                };

                if ((prm & 1) == 1)
                {
                    var index = prm >> 1;
                    if (index < modifiers.Count)
                        piece.Modifier = modifiers[index];
                }

                _pieces.Add(piece);
            }
        }
    }
}
