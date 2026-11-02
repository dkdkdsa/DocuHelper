using System;
using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocStyleSheet
    {
        private const int MaxStyles = 1 << 12;
        private const int MaxDepth = 32;
        private const int ParagraphKind = 1;
        private const int CharacterKind = 2;

        private readonly Dictionary<int, DocStyle> _styles = new();

        public int DefaultAscii { get; private set; } = -1;

        public int DefaultEastAsia { get; private set; } = -1;

        public int DefaultOther { get; private set; } = -1;

        public IEnumerable<DocStyle> All => _styles.Values;

        public static DocStyleSheet Read(byte[] table, DocRange range)
        {
            var sheet = new DocStyleSheet();
            if (range.Length < 2 || !DocBytes.Has(table, range.Offset, range.Length))
                return sheet;

            var end = range.Offset + range.Length;
            var headerSize = DocBytes.U16(table, range.Offset);
            var header = range.Offset + 2;
            if (headerSize < 4 || header + headerSize > end)
                return sheet;

            var count = Math.Min(DocBytes.U16(table, header), MaxStyles);
            var baseSize = DocBytes.U16(table, header + 2);
            if (headerSize >= 18)
            {
                sheet.DefaultAscii = DocBytes.U16(table, header + 12);
                sheet.DefaultEastAsia = DocBytes.U16(table, header + 14);
                sheet.DefaultOther = DocBytes.U16(table, header + 16);
            }

            var position = header + headerSize;
            for (var i = 0; i < count && position + 2 <= end; i++)
            {
                var size = DocBytes.U16(table, position);
                position += 2;
                if (size > 0 && position + size <= end)
                {
                    var style = ReadStyle(table, position, size, baseSize, i);
                    if (style != null)
                        sheet._styles[i] = style;
                }

                position += size;
            }

            return sheet;
        }

        public DocStyle Find(int index) => _styles.TryGetValue(index, out var style) ? style : null;

        public DocChp ResolveCharacters(int index) => ResolveCharacters(index, 0);

        public DocPap ResolveParagraph(int index) => ResolveParagraph(index, 0);

        private static DocStyle ReadStyle(byte[] table, long position, int size, int baseSize, int index)
        {
            if (baseSize < 10 || baseSize + 4 > size)
                return null;

            var style = new DocStyle { Index = index };
            style.Sti = DocBytes.U16(table, position) & 0x0FFF;
            var second = DocBytes.U16(table, position + 2);
            style.Kind = second & 0x0F;
            style.Base = second >> 4;
            var third = DocBytes.U16(table, position + 4);
            var upxCount = third & 0x0F;
            style.Next = third >> 4;

            var end = position + size;
            var cursor = position + baseSize;
            var length = DocBytes.U16(table, cursor);
            if (cursor + 2 + length * 2L > end)
                return style;

            style.Name = DocText.Clean(DocBytes.Utf16(table, cursor + 2, length));
            cursor += 4 + length * 2L;
            for (var u = 0; u < upxCount && cursor + 2 <= end; u++)
            {
                var upxSize = DocBytes.U16(table, cursor);
                var data = cursor + 2;
                if (data + upxSize > end)
                    break;

                var slice = DocBytes.Slice(table, data, upxSize);
                if (style.Kind == ParagraphKind && u == 0 && slice.Length >= 2)
                    DocPapReader.Apply(style.Pap, slice, 2, slice.Length);
                else if ((style.Kind == ParagraphKind && u == 1) || (style.Kind == CharacterKind && u == 0))
                    DocChpReader.Apply(style.Chp, slice, 0, slice.Length, null);

                cursor = data + upxSize + (upxSize & 1);
            }

            return style;
        }

        private DocChp ResolveCharacters(int index, int depth)
        {
            var style = Find(index);
            if (style == null || depth > MaxDepth)
                return new DocChp();

            if (style.ResolvedChp != null)
                return style.ResolvedChp;

            var resolved = style.Base != DocStyle.NoBase && style.Base != index ? ResolveCharacters(style.Base, depth + 1).Clone() : new DocChp();
            resolved.Overlay(style.Chp);
            if (depth == 0)
                style.ResolvedChp = resolved;

            return resolved;
        }

        private DocPap ResolveParagraph(int index, int depth)
        {
            var style = Find(index);
            if (style == null || depth > MaxDepth)
                return new DocPap();

            if (style.ResolvedPap != null)
                return style.ResolvedPap;

            var resolved = new DocPap();
            if (style.Base != DocStyle.NoBase && style.Base != index)
                resolved.Overlay(ResolveParagraph(style.Base, depth + 1));

            resolved.Overlay(style.Pap);
            if (depth == 0)
                style.ResolvedPap = resolved;

            return resolved;
        }
    }
}
