using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class SortRangeTool : ExcelTool
    {
        private static readonly CompareInfo s_compare = CreateCompare();

        public override string Name => "sort_range";

        public override string Description =>
            "Sort the rows of a range by one column, like Excel's sort. Pass only the data rows (no header row) and include every column of the table so each row stays together. "
            + "Values, formulas and formatting move with their row, and formulas that refer to their own row are adjusted. "
            + "Blank cells in the sort column always go last. Refuses ranges with merged cells or formulas that point at other rows of the range.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "range": {"type": "string", "description": "The data rows to sort, without the header, e.g. \"A2:F40\"."},
                "by": {"type": "string", "description": "Column letter to sort by, inside the range, e.g. \"F\"."},
                "descending": {"type": "boolean", "description": "true for largest first (text 하→가, Z→A). Default false: smallest first."}
              },
              "required": ["sheet", "range", "by"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", true);
            var range = ParseRange(sheet, input.GetString("range"));
            var by = ParseColumn(input.GetString("by"));
            if (by < range.Left || by > range.Right)
                throw new ToolInputException($"정렬 기준 열은 범위 {range} 안에 있어야 해요. 지금은 {CellAddress.ColumnName(by)}열이에요.");

            var descending = input.GetOptionalBool("descending") ?? false;
            if (range.RowCount < 2)
                throw new ToolInputException("정렬하려면 두 줄 이상이 필요해요.");

            if (range.CellCount > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 정렬할 수 있어요.");

            var merged = sheet.MergedRanges().FirstOrDefault(m => m.Intersects(range));
            if (merged != null)
                throw new ToolInputException($"범위 안에 합쳐진 칸({merged})이 있어서 정렬할 수 없어요.");

            var rows = Enumerable.Range(range.Top, range.RowCount).ToList();
            var keys = rows.ToDictionary(r => r, r => KeyOf(book, sheet, by, r));
            var order = rows.OrderBy(r => r, Comparer<int>.Create((a, b) => CompareKeys(keys[a], keys[b], descending))).ToList();
            var target = new Dictionary<int, int>();
            for (var i = 0; i < order.Count; i++)
                target[order[i]] = range.Top + i;

            var moved = target.Count(p => p.Key != p.Value);
            var letter = CellAddress.ColumnName(by);
            if (moved == 0)
                return ToolResult.Ok($"‘{sheet.Name}’ {range} is already in that order by column {letter}, so nothing changed.", "그대로");

            var adjusted = new Dictionary<(int Column, int Row), string>();
            foreach (var entry in sheet.Entries.Where(e => range.Contains(e.Column, e.Row) && e.Cell.CellFormula != null))
            {
                var type = entry.Cell.CellFormula.FormulaType?.InnerText;
                if (type == "array" || type == "dataTable")
                    throw new ToolInputException($"{entry.Address} 칸에 배열 수식이 있어서 정렬할 수 없어요.");

                var formula = sheet.FormulaOf(entry.Cell, entry.Column, entry.Row);
                if (string.IsNullOrEmpty(formula))
                    continue;

                if (!TryAdjust(formula, sheet.Name, range, entry.Row, target, out var text))
                    throw new ToolInputException($"{entry.Address} 칸의 수식(={formula})이 정렬 범위 안의 다른 줄을 가리키고 있어서, 정렬하면 계산이 틀어져요. 정렬하지 않았어요.");

                adjusted[(entry.Column, entry.Row)] = text;
            }

            sheet.PrepareForWrite();
            var snapshot = new Dictionary<(int Column, int Row), Cell>();
            foreach (var address in range.Cells())
            {
                var cell = sheet.FindCell(address.Column, address.Row);
                snapshot[(address.Column, address.Row)] = cell == null ? null : (Cell)cell.CloneNode(true);
            }

            var written = new List<CellKey>();
            foreach (var pair in target.Where(p => p.Key != p.Value))
            {
                var source = pair.Key;
                var destination = pair.Value;
                for (var column = range.Left; column <= range.Right; column++)
                {
                    written.Add(new CellKey(sheet.Index, column, destination));
                    var original = snapshot[(column, source)];
                    if (original == null)
                    {
                        sheet.RemoveCell(column, destination);
                        continue;
                    }

                    var copy = (Cell)original.CloneNode(true);
                    if (copy.CellFormula != null && adjusted.TryGetValue((column, source), out var formula))
                    {
                        copy.CellFormula.FormulaType = null;
                        copy.CellFormula.SharedIndex = null;
                        copy.CellFormula.Reference = null;
                        copy.CellFormula.Text = formula;
                    }

                    sheet.ReplaceCell(column, destination, copy);
                }
            }

            RemapAnchors(docu, sheet.Name, range, target);
            sheet.UpdateDimension();
            var result = Recalculate(book, written);
            var direction = descending ? "largest / Z first" : "smallest / A first";
            var content = $"Sorted ‘{sheet.Name}’ {range} by column {letter} ({direction}); {moved} of {range.RowCount} rows moved, with their formulas and formatting."
                + (result.Updated > 0 ? $" {result.Updated} formula cell(s) elsewhere were recalculated." : string.Empty);

            var change = new DocuChange
            {
                Location = range.ToString(),
                Icon = "sort",
                Anchors = Anchors(sheet, range.Cells()),
            };
            return ToolResult.Changed(content, change, $"{range.RowCount}줄");
        }

        private static CompareInfo CreateCompare()
        {
            try
            {
                return CultureInfo.GetCultureInfo("ko-KR").CompareInfo;
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture.CompareInfo;
            }
        }

        private static int ParseColumn(string text)
        {
            var value = (text ?? string.Empty).Trim().Replace("$", string.Empty);
            if (value.EndsWith("열", StringComparison.Ordinal))
                value = value.Substring(0, value.Length - 1).Trim();

            if (CellAddress.TryParseColumn(value, out var column))
                return column;

            if (CellAddress.TryParse(value, out var address))
                return address.Column;

            throw new ToolInputException($"by 값은 F처럼 열 글자로 알려 주세요. 받은 값: {text}");
        }

        private static ExcelValue KeyOf(ExcelBook book, ExcelSheet sheet, int column, int row)
        {
            var cell = sheet.FindCell(column, row);
            if (cell == null || (cell.CellFormula != null && cell.CellValue == null))
                return ExcelValue.Blank;

            return book.ReadValue(cell);
        }

        private static int CompareKeys(ExcelValue left, ExcelValue right, bool descending)
        {
            if (left.IsBlank && right.IsBlank)
                return 0;

            if (left.IsBlank)
                return 1;

            if (right.IsBlank)
                return -1;

            var leftRank = Rank(left);
            var rightRank = Rank(right);
            var comparison = leftRank != rightRank
                ? leftRank.CompareTo(rightRank)
                : left.Kind switch
                {
                    ExcelValueKind.Number => left.Number.CompareTo(right.Number),
                    ExcelValueKind.Text => s_compare.Compare(left.Text, right.Text, CompareOptions.IgnoreCase),
                    ExcelValueKind.Boolean => left.Boolean.CompareTo(right.Boolean),
                    _ => 0,
                };

            return descending ? -comparison : comparison;
        }

        private static int Rank(ExcelValue value) => value.Kind switch
        {
            ExcelValueKind.Number => 0,
            ExcelValueKind.Text => 1,
            ExcelValueKind.Boolean => 2,
            _ => 3,
        };

        private static bool TryAdjust(string formula, string sheetName, CellRange block, int row, Dictionary<int, int> target, out string adjusted)
        {
            var ok = true;
            var delta = target[row] - row;
            try
            {
                adjusted = FormulaText.Rewrite(formula, reference =>
                {
                    if (!reference.IsOnSheet(sheetName, sheetName) || reference.Kind == FormulaReferenceKind.Columns)
                        return reference;

                    var full = reference.ToFullRange();
                    if (!full.Intersects(block))
                        return reference;

                    if (reference.Kind != FormulaReferenceKind.Rows && full.Top == row && full.Bottom == row && full.Left >= block.Left && full.Right <= block.Right)
                        return reference.MoveRows(delta);

                    var touched = full.Intersect(block);
                    var anyMove = delta != 0 || Enumerable.Range(touched.Top, touched.RowCount).Any(r => target[r] != r);
                    if (anyMove)
                        ok = false;

                    return reference;
                }, out _);
            }
            catch (FormulaException)
            {
                adjusted = formula;
                return delta == 0;
            }

            return ok;
        }

        private static void RemapAnchors(Docu docu, string sheetName, CellRange block, Dictionary<int, int> target)
        {
            foreach (var change in docu.Changes)
            {
                for (var i = 0; i < change.Anchors.Count; i++)
                {
                    if (!ExcelAnchor.TryParse(change.Anchors[i], out var sheet, out var cellRef) || sheet != sheetName)
                        continue;

                    if (!CellAddress.TryParse(cellRef, out var address) || !block.Contains(address.Column, address.Row))
                        continue;

                    change.Anchors[i] = ExcelAnchor.Cell(sheetName, new CellAddress(address.Column, target[address.Row]));
                }
            }
        }
    }
}
