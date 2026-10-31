using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;
using static DocuHelper.Importers.Hwp.HwpWordWriter;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpWordTables
    {
        private const string BorderColor = "000000";

        internal static Table ToTable(HwpTable source, int availableWidth)
        {
            var cells = source.Cells.Where(c => c != null && c.Row >= 0 && c.Column >= 0).ToList();
            var rows = Math.Max(source.RowCount, cells.Count == 0 ? 0 : cells.Max(c => c.Row) + 1);
            var columns = Math.Max(source.ColumnCount, cells.Count == 0 ? 0 : cells.Max(c => c.Column) + 1);
            var owners = new HwpCell[rows, columns];
            var spans = new Dictionary<HwpCell, (int Rows, int Columns)>();
            foreach (var cell in cells.OrderBy(c => c.Row).ThenBy(c => c.Column))
            {
                if (owners[cell.Row, cell.Column] != null)
                    continue;

                var rowSpan = Math.Clamp(cell.RowSpan, 1, rows - cell.Row);
                var columnSpan = Math.Clamp(cell.ColumnSpan, 1, columns - cell.Column);
                spans[cell] = (rowSpan, columnSpan);
                for (var r = cell.Row; r < cell.Row + rowSpan; r++)
                {
                    for (var c = cell.Column; c < cell.Column + columnSpan; c++)
                    {
                        if (owners[r, c] == null)
                            owners[r, c] = cell;
                    }
                }
            }

            var widths = ColumnWidths(cells, spans, columns, availableWidth);
            var table = new Table(new TableProperties(
                new TableWidth { Width = Number(widths.Sum()), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = BorderColor },
                    new LeftBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = BorderColor },
                    new BottomBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = BorderColor },
                    new RightBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = BorderColor },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = BorderColor },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = BorderColor }),
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableCellMarginDefault(
                    new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                    new TableCellLeftMargin { Width = 100, Type = TableWidthValues.Dxa },
                    new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                    new TableCellRightMargin { Width = 100, Type = TableWidthValues.Dxa })));

            var grid = new TableGrid();
            foreach (var width in widths)
                grid.AppendChild(new GridColumn { Width = Number(width) });

            table.AppendChild(grid);

            for (var r = 0; r < rows; r++)
            {
                var row = new TableRow();
                var c = 0;
                while (c < columns)
                {
                    var owner = owners[r, c];
                    if (owner == null)
                    {
                        row.AppendChild(NewCell(widths[c], 1, null, Array.Empty<HwpBlock>()));
                        c++;
                        continue;
                    }

                    if (owner.Column != c)
                    {
                        c++;
                        continue;
                    }

                    var span = spans[owner];
                    var width = widths.Skip(c).Take(span.Columns).Sum();
                    if (owner.Row == r)
                        row.AppendChild(NewCell(width, span.Columns, span.Rows > 1 ? MergedCellValues.Restart : null, owner.Blocks));
                    else
                        row.AppendChild(NewCell(width, span.Columns, MergedCellValues.Continue, Array.Empty<HwpBlock>()));

                    c += span.Columns;
                }

                table.AppendChild(row);
            }

            return table;
        }

        private static TableCell NewCell(int width, int columnSpan, MergedCellValues? merge, IEnumerable<HwpBlock> blocks)
        {
            var properties = new TableCellProperties(new TableCellWidth { Width = Number(width), Type = TableWidthUnitValues.Dxa });
            if (columnSpan > 1)
                properties.AppendChild(new GridSpan { Val = columnSpan });

            if (merge == MergedCellValues.Restart)
                properties.AppendChild(new VerticalMerge { Val = MergedCellValues.Restart });
            else if (merge == MergedCellValues.Continue)
                properties.AppendChild(new VerticalMerge());

            var cell = new TableCell(properties);
            foreach (var element in ToElements(blocks, Math.Max(MinColumnWidth, width - 200)))
                cell.AppendChild(element);

            if (cell.LastChild is not Paragraph)
                cell.AppendChild(new Paragraph());

            return cell;
        }

        private static int[] ColumnWidths(List<HwpCell> cells, Dictionary<HwpCell, (int Rows, int Columns)> spans, int columns, int availableWidth)
        {
            var widths = new int[columns];
            foreach (var cell in cells.Where(c => spans.ContainsKey(c) && spans[c].Columns == 1 && c.Width > 0))
            {
                if (widths[cell.Column] == 0)
                    widths[cell.Column] = Twips(cell.Width);
            }

            foreach (var cell in cells.Where(c => spans.ContainsKey(c) && spans[c].Columns > 1 && c.Width > 0))
            {
                var range = Enumerable.Range(cell.Column, spans[cell].Columns).ToList();
                var unknown = range.Where(i => widths[i] == 0).ToList();
                if (unknown.Count == 0)
                    continue;

                var rest = Twips(cell.Width) - range.Sum(i => widths[i]);
                if (rest <= 0)
                    continue;

                foreach (var i in unknown)
                    widths[i] = rest / unknown.Count;
            }

            var known = widths.Where(w => w > 0).ToList();
            var fallback = known.Count > 0 ? (int)known.Average() : availableWidth / Math.Max(1, columns);
            for (var i = 0; i < columns; i++)
                widths[i] = Math.Max(MinColumnWidth, widths[i] > 0 ? widths[i] : fallback);

            return widths;
        }
    }
}
