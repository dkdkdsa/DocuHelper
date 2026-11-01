using System;
using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocSectionTable
    {
        private const int MaxSections = 1 << 16;

        public static List<DocSection> Read(byte[] word, byte[] table, DocRange plc, int length)
        {
            var sections = new List<DocSection>();
            var count = plc.Length >= 4 && DocBytes.Has(table, plc.Offset, plc.Length) ? (plc.Length - 4) / 16 : 0;
            if (count > MaxSections)
                throw DocBytes.Broken();

            var previous = 0;
            for (var i = 0; i < count; i++)
            {
                var end = DocBytes.I32(table, plc.Offset + (i + 1) * 4);
                if (end <= previous && i > 0)
                    continue;

                var section = new DocSection { CpEnd = Math.Min(end, length) };
                var at = DocBytes.U32(table, plc.Offset + (count + 1) * 4 + i * 12 + 2);
                if (at != 0xFFFFFFFF && DocBytes.Has(word, at, 2))
                {
                    var size = DocBytes.U16(word, at);
                    if (DocBytes.Has(word, at + 2, size))
                        Apply(section, word, (int)at + 2, (int)at + 2 + size);
                }

                sections.Add(section);
                previous = end;
            }

            if (sections.Count == 0)
                sections.Add(new DocSection());

            sections[^1].CpEnd = length;
            return sections;
        }

        private static void Apply(DocSection section, byte[] data, int start, int end)
        {
            var position = start;
            while (DocSprmReader.Next(data, ref position, end, out var sprm))
            {
                var o = sprm.Offset;
                switch (sprm.Op)
                {
                    case 0xB01F:
                        section.Width = Word(data, o);
                        break;
                    case 0xB020:
                        section.Height = Word(data, o);
                        break;
                    case 0xB021:
                        section.Left = Word(data, o);
                        break;
                    case 0xB022:
                        section.Right = Word(data, o);
                        break;
                    case 0x9023:
                        section.Top = Math.Abs((short)Word(data, o));
                        break;
                    case 0x9024:
                        section.Bottom = Math.Abs((short)Word(data, o));
                        break;
                    case 0xB025:
                        section.Gutter = Word(data, o);
                        break;
                    case 0xB017:
                        section.HeaderDistance = Word(data, o);
                        break;
                    case 0xB018:
                        section.FooterDistance = Word(data, o);
                        break;
                    case 0x301D:
                        section.Landscape = data[o] == 2;
                        break;
                    case 0x3009:
                        section.BreakKind = data[o];
                        break;
                    case 0x500B:
                        section.Columns = Math.Clamp(Word(data, o) + 1, 1, 45);
                        break;
                    case 0x900C:
                        section.ColumnSpace = Word(data, o);
                        break;
                }
            }
        }

        private static int Word(byte[] data, int offset) => data[offset] | (data[offset + 1] << 8);
    }
}
