using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocFontTable
    {
        private const int NameOffset = 40;
        private const int MaxFonts = 1 << 14;

        private readonly List<string> _names = new();

        public static DocFontTable Read(byte[] table, DocRange range)
        {
            var fonts = new DocFontTable();
            if (range.Length < 4 || !DocBytes.Has(table, range.Offset, range.Length))
                return fonts;

            var count = DocBytes.U16(table, range.Offset);
            var position = range.Offset + 4;
            var end = range.Offset + range.Length;
            for (var i = 0; i < count && i < MaxFonts && position < end; i++)
            {
                var size = DocBytes.U8(table, position) + 1;
                if (position + size > end)
                    break;

                string name = null;
                if (size > NameOffset + 2)
                {
                    var chars = 0;
                    var limit = (size - NameOffset) / 2;
                    while (chars < limit && DocBytes.U16(table, position + NameOffset + chars * 2) != 0)
                        chars++;

                    name = DocText.Clean(DocBytes.Utf16(table, position + NameOffset, chars)).Trim();
                }

                fonts._names.Add(string.IsNullOrEmpty(name) ? null : name);
                position += size;
            }

            return fonts;
        }

        public string Name(int? index) => index.HasValue && index.Value >= 0 && index.Value < _names.Count ? _names[index.Value] : null;
    }
}
