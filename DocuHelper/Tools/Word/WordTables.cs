using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordTables
    {
        private const string BorderColor = "999999";
        private const string HeaderFill = "F2F2F2";

        private static readonly Regex s_number = new(
            @"^\(?[+\-−±]?[₩$€£¥]?\s*\d[\d,]*(\.\d+)?\s*(%|％|원|만|억|천|만\s?원|억\s?원|천\s?원|개|건|명|회|배|시간|일|kg|g|km|m|점|p|pt)?\)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string IdOf(Table table) => WordIds.Of(table?.Descendants<Paragraph>().FirstOrDefault());

        public static IEnumerable<TableRow> Rows(Table table)
        {
            foreach (var child in table.ChildElements)
            {
                switch (child)
                {
                    case TableRow row:
                        yield return row;
                        break;
                    case SdtRow sdt when sdt.SdtContentRow != null:
                        foreach (var row in sdt.SdtContentRow.Elements<TableRow>())
                            yield return row;
                        break;
                    case CustomXmlRow custom:
                        foreach (var row in custom.Elements<TableRow>())
                            yield return row;
                        break;
                }
            }
        }

        public static List<List<WordCell>> Grid(Table table)
        {
            var grid = new List<List<WordCell>>();
            foreach (var row in Rows(table))
            {
                var cells = new List<WordCell>();
                var before = row.TableRowProperties?.GetFirstChild<GridBefore>()?.Val?.Value ?? 0;
                for (var i = 0; i < before; i++)
                    cells.Add(new WordCell());

                foreach (var cell in row.Elements<TableCell>())
                {
                    var span = Math.Max(1, cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1);
                    var merge = cell.TableCellProperties?.VerticalMerge;
                    var continues = merge != null && (merge.Val == null || merge.Val.Value == MergedCellValues.Continue);
                    var owner = continues ? OwnerAbove(grid, cells.Count) ?? cell : cell;
                    for (var s = 0; s < span; s++)
                        cells.Add(new WordCell { Cell = cell, Owner = owner, IsOrigin = s == 0 && !continues });
                }

                grid.Add(cells);
            }

            return grid;
        }

        public static List<List<string>> Texts(List<List<WordCell>> grid) =>
            grid.Select(row => row.Select(c => c.IsOrigin ? CellText(c.Owner) : string.Empty).ToList()).ToList();

        public static string CellText(TableCell cell) =>
            cell == null ? string.Empty : string.Join("\n", cell.Elements<Paragraph>().Select(WordText.Of));

        public static int ColumnCount(List<List<WordCell>> grid) => grid.Count == 0 ? 0 : grid.Max(r => r.Count);

        public static bool LooksNumeric(string text)
        {
            var value = (text ?? string.Empty).Trim();
            return value.Length > 0 && s_number.IsMatch(value);
        }

        public static bool IsPlaceholder(string text)
        {
            var value = (text ?? string.Empty).Trim();
            return value.Length == 0 || value == "-" || value == "–" || value == "—";
        }

        public static List<int> NumericColumns(IReadOnlyList<IReadOnlyList<string>> rows, bool header)
        {
            var result = new List<int>();
            var columns = rows.Count == 0 ? 0 : rows.Max(r => r?.Count ?? 0);
            var first = header ? 1 : 0;
            for (var c = 0; c < columns; c++)
            {
                var values = new List<string>();
                for (var r = first; r < rows.Count; r++)
                {
                    var row = rows[r];
                    if (row != null && c < row.Count && !IsPlaceholder(row[c]))
                        values.Add(row[c]);
                }

                if (values.Count > 0 && values.All(LooksNumeric))
                    result.Add(c);
            }

            return result;
        }

        public static bool HasHeaderRow(WordStyles styles, Table table)
        {
            var row = Rows(table).FirstOrDefault();
            if (row == null)
                return false;

            var header = row.TableRowProperties?.GetFirstChild<TableHeader>();
            if (header != null && (header.Val == null || header.Val.Value == OnOffOnlyValues.On))
                return true;

            var cells = row.Elements<TableCell>().ToList();
            if (cells.Count == 0)
                return false;

            if (cells.All(IsShaded))
                return true;

            var runs = cells.SelectMany(c => c.Elements<Paragraph>()).SelectMany(p => WordText.TextRuns(p).Select(r => (Run: r, Paragraph: p))).ToList();
            return runs.Count > 0 && runs.All(x => WordStyles.IsOn(styles.RunValue<Bold>(x.Run, x.Paragraph)));
        }

        public static Table Build(IReadOnlyList<IReadOnlyList<string>> rows, bool header, int width, HashSet<string> used)
        {
            var columns = Math.Max(1, rows.Max(r => r.Count));
            var numeric = NumericColumns(rows, header);
            var cellWidth = Math.Max(400, width / columns);
            var cellText = cellWidth.ToString(CultureInfo.InvariantCulture);

            var table = new Table();
            table.AppendChild(new TableProperties(
                new TableWidth { Width = (cellWidth * columns).ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
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
            for (var c = 0; c < columns; c++)
                grid.AppendChild(new GridColumn { Width = cellText });

            table.AppendChild(grid);

            for (var r = 0; r < rows.Count; r++)
            {
                var isHeader = header && r == 0;
                var row = new TableRow();
                if (isHeader)
                    row.AppendChild(new TableRowProperties(new TableHeader()));

                for (var c = 0; c < columns; c++)
                {
                    var text = c < rows[r].Count ? rows[r][c] ?? string.Empty : string.Empty;
                    var properties = new TableCellProperties(new TableCellWidth { Width = cellText, Type = TableWidthUnitValues.Dxa });
                    if (isHeader)
                        properties.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = HeaderFill });

                    row.AppendChild(new TableCell(properties, CellParagraph(text, isHeader, numeric.Contains(c), used)));
                }

                table.AppendChild(row);
            }

            return table;
        }

        public static Paragraph CellParagraph(string text, bool bold, bool right, HashSet<string> used)
        {
            var paragraph = new Paragraph();
            var properties = new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto });
            if (right)
                properties.AppendChild(new Justification { Val = JustificationValues.Right });

            if (bold)
                properties.ParagraphMarkRunProperties = new ParagraphMarkRunProperties(new Bold(), new BoldComplexScript());

            paragraph.AppendChild(properties);
            var value = WordText.Clean(text);
            if (value.Length > 0)
                paragraph.AppendChild(WordText.NewRun(value, bold ? new RunProperties(new Bold(), new BoldComplexScript()) : null));

            WordIds.Assign(paragraph, used);
            return paragraph;
        }

        public static TableCell CellAt(List<List<WordCell>> grid, int row, int column)
        {
            if (row < 0 || row >= grid.Count || column < 0 || column >= grid[row].Count)
                return null;

            return grid[row][column].Owner;
        }

        private static TableCell OwnerAbove(List<List<WordCell>> grid, int column)
        {
            for (var r = grid.Count - 1; r >= 0; r--)
            {
                if (column < grid[r].Count && grid[r][column].Owner != null)
                    return grid[r][column].Owner;
            }

            return null;
        }

        private static bool IsShaded(TableCell cell)
        {
            var fill = cell.TableCellProperties?.Shading?.Fill?.Value;
            return !string.IsNullOrEmpty(fill)
                && !string.Equals(fill, "auto", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fill, "FFFFFF", StringComparison.OrdinalIgnoreCase);
        }
    }
}
