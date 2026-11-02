using System;
using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocListTable
    {
        private const int MaxLists = 1 << 14;
        private const int ListHeaderSize = 28;
        private const int LevelHeaderSize = 28;
        private const int OverrideSize = 16;
        private const int LevelCount = 9;

        public List<DocListDefinition> Definitions { get; } = new();

        public List<DocListOverride> Overrides { get; } = new();

        public static DocListTable Read(byte[] table, DocRange lists, DocRange overrides)
        {
            var result = new DocListTable();
            try
            {
                result.ReadLists(table, lists, overrides);
                result.ReadOverrides(table, overrides);
            }
            catch (Exception ex) when (ex is System.IO.InvalidDataException || ex is ArgumentException || ex is IndexOutOfRangeException)
            {
                result.Overrides.Clear();
                result.Definitions.Clear();
            }

            return result;
        }

        private static DocListLevel ReadLevel(byte[] table, ref long position, long end)
        {
            if (position + LevelHeaderSize > end)
                throw DocBytes.Broken();

            var level = new DocListLevel
            {
                Start = DocBytes.I32(table, position),
                Format = DocBytes.U8(table, position + 4),
                Justification = DocBytes.U8(table, position + 5) & 3,
                Follow = DocBytes.U8(table, position + 15),
            };

            var characterSize = DocBytes.U8(table, position + 24);
            var paragraphSize = DocBytes.U8(table, position + 25);
            position += LevelHeaderSize;
            if (position + paragraphSize + characterSize + 2 > end)
                throw DocBytes.Broken();

            var paragraph = DocBytes.Slice(table, position, paragraphSize);
            DocPapReader.Apply(level.Pap, paragraph, 0, paragraph.Length);
            position += paragraphSize;
            var character = DocBytes.Slice(table, position, characterSize);
            DocChpReader.Apply(level.Chp, character, 0, character.Length, null);
            position += characterSize;

            var chars = DocBytes.U16(table, position);
            position += 2;
            if (position + chars * 2L > end)
                throw DocBytes.Broken();

            level.Text = DocBytes.Utf16(table, position, chars);
            position += chars * 2L;
            return level;
        }

        private void ReadLists(byte[] table, DocRange range, DocRange overrides)
        {
            if (range.Length < 2 || !DocBytes.Has(table, range.Offset, range.Length))
                return;

            var end = range.Offset + range.Length;
            var count = DocBytes.U16(table, range.Offset);
            if (count > MaxLists || range.Offset + 2 + (long)count * ListHeaderSize > end)
                return;

            var levelsEnd = overrides.Offset >= end && overrides.Offset <= table.Length ? overrides.Offset : table.Length;

            var headers = range.Offset + 2;
            var position = headers + (long)count * ListHeaderSize;
            for (var i = 0; i < count; i++)
            {
                var header = headers + (long)i * ListHeaderSize;
                var definition = new DocListDefinition { Id = DocBytes.U32(table, header) };
                var simple = (DocBytes.U8(table, header + 26) & 1) != 0;
                var levels = simple ? 1 : LevelCount;
                for (var level = 0; level < levels; level++)
                    definition.Levels.Add(ReadLevel(table, ref position, levelsEnd));

                Definitions.Add(definition);
            }
        }

        private void ReadOverrides(byte[] table, DocRange range)
        {
            if (range.Length < 4 || !DocBytes.Has(table, range.Offset, range.Length))
                return;

            var end = range.Offset + range.Length;
            var count = DocBytes.I32(table, range.Offset);
            if (count < 0 || count > MaxLists || range.Offset + 4 + (long)count * OverrideSize > end)
                return;

            var headers = range.Offset + 4;
            var position = headers + (long)count * OverrideSize;
            for (var i = 0; i < count; i++)
            {
                var header = headers + (long)i * OverrideSize;
                var item = new DocListOverride { ListId = DocBytes.U32(table, header) };
                var levels = DocBytes.U8(table, header + 12);
                position += 4;
                for (var k = 0; k < levels; k++)
                {
                    var start = DocBytes.I32(table, position);
                    var flags = DocBytes.U32(table, position + 4);
                    position += 8;
                    var entry = new DocLevelOverride { Level = (int)(flags & 0x0F) };
                    if ((flags & 0x10) != 0)
                        entry.StartAt = start;

                    if ((flags & 0x20) != 0)
                        entry.Format = ReadLevel(table, ref position, end);

                    item.Levels.Add(entry);
                }

                Overrides.Add(item);
            }
        }
    }
}
