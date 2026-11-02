namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocTapReader
    {
        private const int CellSize = 20;
        private const int MaxColumns = 64;

        public static void Apply(DocPap pap, byte[] data, DocSprm sprm)
        {
            var o = sprm.Offset;
            var length = sprm.Length;
            switch (sprm.Op)
            {
                case 0xD608:
                    Row(pap).Cells.Clear();
                    Row(pap).Centers.Clear();
                    ReadDefinition(Row(pap), data, o, length);
                    break;
                case 0xD605:
                    if (length >= 24)
                        ReadBorders(Row(pap), data, o, 4, true);

                    break;
                case 0xD613:
                    if (length >= 48)
                        ReadBorders(Row(pap), data, o, 8, false);

                    break;
                case 0x9407:
                    Row(pap).RowHeight = (short)Word(data, o);
                    break;
                case 0x3404:
                    Row(pap).Header = data[o] != 0;
                    break;
                case 0x9602:
                    Row(pap).Gap = (short)Word(data, o);
                    break;
                case 0x5400:
                    Row(pap).Justification = Word(data, o);
                    break;
                case 0xD609:
                    for (var i = 0; i < length / 2 && i < MaxColumns; i++)
                        Row(pap).Cell(i).Shading = DocShading.FromShd80(data, o + i * 2);

                    break;
                case 0xD612:
                    ReadShading(Row(pap), data, o, length, 0);
                    break;
                case 0xD616:
                    ReadShading(Row(pap), data, o, length, 22);
                    break;
                case 0xD670:
                    ReadShading(Row(pap), data, o, length, 44);
                    break;
                case 0x7627:
                    if (length >= 4)
                        SetShading(Row(pap), data[o], data[o + 1], DocShading.FromShd80(data, o + 2));

                    break;
                case 0xD62B:
                    if (length >= 12)
                        SetShading(Row(pap), data[o], data[o + 1], DocShading.FromShd(data, o + 2));

                    break;
                case 0xD620:
                    if (length >= 7)
                        SetBorder(Row(pap), data[o], data[o + 1], data[o + 2], DocBrc.Read80(data, o + 3));

                    break;
            }
        }

        private static DocTap Row(DocPap pap) => pap.Row ??= new DocTap();

        private static int Word(byte[] data, int offset) => data[offset] | (data[offset + 1] << 8);

        private static void ReadDefinition(DocTap row, byte[] data, int offset, int length)
        {
            if (length < 1)
                return;

            var count = System.Math.Min((int)data[offset], MaxColumns);
            var centers = offset + 1;
            if (length < 1 + (count + 1) * 2)
                return;

            for (var i = 0; i <= count; i++)
                row.Centers.Add((short)Word(data, centers + i * 2));

            var cells = centers + (count + 1) * 2;
            var available = (offset + length - cells) / CellSize;
            for (var i = 0; i < count; i++)
            {
                var cell = new DocTc();
                if (i < available)
                {
                    var c = cells + i * CellSize;
                    cell.Flags = Word(data, c);
                    cell.Top = Brc(data, c + 4);
                    cell.Left = Brc(data, c + 8);
                    cell.Bottom = Brc(data, c + 12);
                    cell.Right = Brc(data, c + 16);
                }

                row.Cells.Add(cell);
            }
        }

        private static DocBrc Brc(byte[] data, int offset)
        {
            var brc = DocBrc.Read80(data, offset);
            return brc.IsSet ? brc : null;
        }

        private static void ReadBorders(DocTap row, byte[] data, int offset, int size, bool legacy)
        {
            for (var i = 0; i < 6; i++)
            {
                var brc = legacy ? DocBrc.Read80(data, offset + i * size) : DocBrc.Read(data, offset + i * size);
                row.Borders[i] = brc.IsSet ? brc : null;
            }
        }

        private static void ReadShading(DocTap row, byte[] data, int offset, int length, int first)
        {
            for (var i = 0; i < length / 10 && first + i < MaxColumns; i++)
                row.Cell(first + i).Shading = DocShading.FromShd(data, offset + i * 10);
        }

        private static void SetShading(DocTap row, int first, int limit, int color)
        {
            for (var i = first; i < limit && i < MaxColumns; i++)
                row.Cell(i).Shading = color;
        }

        private static void SetBorder(DocTap row, int first, int limit, int sides, DocBrc brc)
        {
            if (!brc.IsSet)
                brc = null;

            for (var i = first; i < limit && i < MaxColumns; i++)
            {
                var cell = row.Cell(i);
                if ((sides & 1) != 0)
                    cell.Top = brc;

                if ((sides & 2) != 0)
                    cell.Left = brc;

                if ((sides & 4) != 0)
                    cell.Bottom = brc;

                if ((sides & 8) != 0)
                    cell.Right = brc;
            }
        }
    }
}
