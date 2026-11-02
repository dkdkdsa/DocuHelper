using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocWordTables
    {
        private const int Tolerance = 6;
        private const int MinColumn = 20;

        public static Table Build(List<DocRow> rows, int fallbackWidth)
        {
            var pairs = rows.Select(r => (Row: r, Cells: Specs(r, fallbackWidth))).Where(p => p.Cells.Count > 0).ToList();
            if (pairs.Count == 0)
                return null;

            var specs = pairs.Select(p => p.Cells).ToList();

            var grid = Grid(specs);
            foreach (var row in specs)
                Place(row, grid);

            var gap = pairs[0].Row.Properties?.Gap ?? DocTap.DefaultGap;
            var widths = new int[grid.Count - 1];
            for (var i = 0; i < widths.Length; i++)
                widths[i] = Math.Max(MinColumn, grid[i + 1] - grid[i]);

            var table = new Table(new TableProperties(
                new TableWidth { Width = DocWordRuns.Number(widths.Sum()), Type = TableWidthUnitValues.Dxa },
                new TableIndentation { Width = Math.Clamp(grid[0], -5000, 5000), Type = TableWidthUnitValues.Dxa },
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableCellMarginDefault(
                    new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new TableCellLeftMargin { Width = (short)Math.Clamp(gap, 0, 1000), Type = TableWidthValues.Dxa },
                    new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new TableCellRightMargin { Width = (short)Math.Clamp(gap, 0, 1000), Type = TableWidthValues.Dxa })));

            var tableGrid = new TableGrid();
            foreach (var width in widths)
                tableGrid.AppendChild(new GridColumn { Width = DocWordRuns.Number(width) });

            table.AppendChild(tableGrid);
            for (var r = 0; r < specs.Count; r++)
                table.AppendChild(Row(pairs[r].Row.Properties, specs[r], widths, r, specs.Count));

            return table;
        }

        private static List<DocCellSpec> Specs(DocRow row, int fallbackWidth)
        {
            var tap = row.Properties;
            var centers = tap != null && tap.Centers.Count >= 2 ? tap.Centers : null;
            var count = centers != null ? centers.Count - 1 : row.Cells.Count;
            var result = new List<DocCellSpec>();
            if (count <= 0)
                return result;

            var contents = row.Cells.Select(c => c).ToList();
            while (contents.Count > count)
            {
                contents[count - 1].AddRange(contents[count]);
                contents.RemoveAt(count);
            }

            while (contents.Count < count)
                contents.Add(new List<OpenXmlElement>());

            var step = Math.Max(MinColumn, fallbackWidth / count);
            for (var i = 0; i < count; i++)
            {
                var tc = tap != null && i < tap.Cells.Count ? tap.Cells[i] : new DocTc();
                var spec = new DocCellSpec
                {
                    Left = centers != null ? centers[i] : i * step,
                    Right = centers != null ? centers[i + 1] : (i + 1) * step,
                    Properties = tc,
                    Content = contents[i],
                };

                var previous = result.Count > 0 ? result[^1] : null;
                if (previous != null && tc.Merged && !tc.FirstMerged)
                {
                    previous.Right = spec.Right;
                    if (spec.Content.Any(e => !string.IsNullOrEmpty(e.InnerText)))
                        previous.Content.AddRange(spec.Content);

                    continue;
                }

                result.Add(spec);
            }

            return result;
        }

        private static List<int> Grid(List<List<DocCellSpec>> rows)
        {
            var values = rows.SelectMany(r => r).SelectMany(c => new[] { c.Left, c.Right }).OrderBy(v => v).ToList();
            var grid = new List<int>();
            foreach (var value in values)
            {
                if (grid.Count == 0 || value - grid[^1] > Tolerance)
                    grid.Add(value);
            }

            if (grid.Count < 2)
                grid.Add(grid[0] + MinColumn);

            return grid;
        }

        private static void Place(List<DocCellSpec> row, List<int> grid)
        {
            var previous = Index(grid, row[0].Left);
            foreach (var cell in row)
            {
                cell.GridStart = previous;
                cell.GridEnd = Math.Max(previous + 1, Index(grid, cell.Right));
                cell.GridEnd = Math.Min(cell.GridEnd, grid.Count - 1);
                if (cell.GridEnd <= cell.GridStart)
                    cell.GridStart = Math.Max(0, cell.GridEnd - 1);

                previous = cell.GridEnd;
            }
        }

        private static int Index(List<int> grid, int value)
        {
            var best = 0;
            for (var i = 1; i < grid.Count; i++)
            {
                if (Math.Abs(grid[i] - value) < Math.Abs(grid[best] - value))
                    best = i;
            }

            return best;
        }

        private static TableRow Row(DocTap tap, List<DocCellSpec> cells, int[] widths, int index, int count)
        {
            var row = new TableRow();
            var properties = new TableRowProperties();
            var before = cells[0].GridStart;
            var after = widths.Length - cells[^1].GridEnd;
            if (before > 0)
                properties.AppendChild(new GridBefore { Val = before });

            if (after > 0)
                properties.AppendChild(new GridAfter { Val = after });

            if (tap != null && tap.RowHeight != 0)
            {
                properties.AppendChild(new TableRowHeight
                {
                    Val = (uint)Math.Min(Math.Abs(tap.RowHeight), 31680),
                    HeightType = tap.RowHeight < 0 ? HeightRuleValues.Exact : HeightRuleValues.AtLeast,
                });
            }

            if (tap != null && tap.Header)
                properties.AppendChild(new TableHeader());

            if (properties.HasChildren)
                row.AppendChild(properties);

            foreach (var cell in cells)
                row.AppendChild(Cell(tap, cell, widths, index, count));

            return row;
        }

        private static TableCell Cell(DocTap tap, DocCellSpec spec, int[] widths, int rowIndex, int rowCount)
        {
            var tc = spec.Properties;
            var width = 0;
            for (var i = spec.GridStart; i < spec.GridEnd && i < widths.Length; i++)
                width += widths[i];

            var properties = new TableCellProperties(new TableCellWidth { Width = DocWordRuns.Number(width), Type = TableWidthUnitValues.Dxa });
            if (spec.GridEnd - spec.GridStart > 1)
                properties.AppendChild(new GridSpan { Val = spec.GridEnd - spec.GridStart });

            if (tc.VerticalRestart)
                properties.AppendChild(new VerticalMerge { Val = MergedCellValues.Restart });
            else if (tc.VerticalMerge)
                properties.AppendChild(new VerticalMerge());

            var last = spec.GridEnd >= widths.Length;
            var borders = new TableCellBorders();
            Side<TopBorder>(borders, tc.Top ?? tap?.Borders[rowIndex == 0 ? 0 : 4]);
            Side<LeftBorder>(borders, tc.Left ?? tap?.Borders[spec.GridStart == 0 ? 1 : 5]);
            Side<BottomBorder>(borders, tc.Bottom ?? tap?.Borders[rowIndex == rowCount - 1 ? 2 : 4]);
            Side<RightBorder>(borders, tc.Right ?? tap?.Borders[last ? 3 : 5]);
            if (borders.HasChildren)
                properties.AppendChild(borders);

            if (tc.Shading >= 0)
                properties.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = DocColors.Hex(tc.Shading) });

            if (tc.VerticalAlign == 1)
                properties.AppendChild(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            else if (tc.VerticalAlign == 2)
                properties.AppendChild(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Bottom });

            var cell = new TableCell(properties);
            if (tc.VerticalMerge && !tc.VerticalRestart)
            {
                cell.AppendChild(new Paragraph());
                return cell;
            }

            foreach (var element in spec.Content)
                cell.AppendChild(element);

            if (cell.LastChild is not Paragraph)
                cell.AppendChild(new Paragraph());

            return cell;
        }

        private static void Side<T>(TableCellBorders borders, DocBrc brc)
            where T : BorderType, new()
        {
            if (brc != null && brc.IsSet)
                borders.AppendChild(DocWordBorders.Create<T>(brc, false));
        }
    }
}
