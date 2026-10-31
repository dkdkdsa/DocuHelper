using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class RemoveDuplicatesTool : ExcelTool
    {
        private const int MaxAnchors = 500;

        public override string Name => "remove_duplicates";

        public override string Description =>
            "Remove duplicate rows from a table, like Excel 중복된 항목 제거. Rows count as duplicates when all chosen columns hold the same displayed values (case-insensitive); the first of each group stays. "
            + "Pass the whole table with every column so each row stays together. The remaining rows move up inside the range and the freed bottom rows are emptied (a table keeps its size; shrink it with manage_table). "
            + "Refuses ranges with merged cells, and formulas that point at only some rows of the range from outside it or at other rows of the range from inside it.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "range": {"type": "string", "description": "The table, e.g. \"A1:F41\"."},
                "has_header": {"type": "boolean", "description": "The first row is a header and is kept. Default true."},
                "columns": {
                  "type": "array",
                  "description": "Columns (letters or header texts) that make two rows equal. Default all columns of range.",
                  "items": {"type": "string", "description": "Column."}
                }
              },
              "required": ["sheet", "range"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", true);
            var range = ParseRange(sheet, input.GetString("range"));
            var hasHeader = input.GetOptionalBool("has_header") ?? true;
            var data = hasHeader ? new CellRange(range.Left, Math.Min(range.Top + 1, range.Bottom), range.Right, range.Bottom) : range;
            if (range.RowCount < 2)
                throw new ToolInputException(hasHeader ? "머리글 줄 아래에 데이터가 있어야 중복을 찾을 수 있어요." : "중복을 찾으려면 두 줄 이상이 필요해요.");

            if (range.CellCount > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 다룰 수 있어요.");

            var columns = ReadColumns(book, sheet, range, input, hasHeader);
            var merged = sheet.MergedRanges().FirstOrDefault(m => m.Intersects(data));
            if (merged != null)
                throw new ToolInputException($"범위 안에 합쳐진 칸({merged})이 있어서 중복을 지울 수 없어요.");

            var kept = new List<int>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var row = data.Top; row <= data.Bottom; row++)
            {
                if (seen.Add(KeyOf(book, sheet, columns, row)))
                    kept.Add(row);
            }

            var removed = data.RowCount - kept.Count;
            if (removed == 0)
                return ToolResult.Ok($"No duplicate rows in ‘{sheet.Name}’ {data} (compared {Describe(columns)}), so nothing changed.", "중복 없음");

            CheckOutside(book, sheet, data);
            var target = new Dictionary<int, int>();
            for (var i = 0; i < kept.Count; i++)
                target[kept[i]] = data.Top + i;

            var adjusted = RowBlock.PlanFormulas(sheet, data, target, "중복 제거");
            var written = RowBlock.Move(sheet, data, target, adjusted, RowBlock.UnderFilter(sheet, data));
            RowBlock.RemapAnchors(docu, sheet.Name, data, target);
            sheet.UpdateDimension();
            var result = Recalculate(book, written);

            var firstFree = data.Top + kept.Count;
            var table = ExcelTables.On(sheet).FirstOrDefault(t => t.Range.Intersects(range));
            var content = $"Removed {removed} duplicate row(s) from ‘{sheet.Name}’ {data} (compared {Describe(columns)}); {kept.Count} unique data row(s) remain in rows {data.Top + 1}-{firstFree}."
                + $" Rows {firstFree + 1}-{data.Bottom + 1} of the range are now empty."
                + (table != null ? $" Table ‘{table.Name}’ keeps its size, so those rows are blank rows inside it; shrink it with manage_table resize if wanted." : string.Empty)
                + (result.Updated > 0 ? $" {result.Updated} formula cell(s) elsewhere were recalculated." : string.Empty);

            var change = new DocuChange
            {
                Location = range.ToString(),
                Icon = "dedupe",
                Anchors = Anchors(sheet, kept.Select((_, i) => new CellAddress(range.Left, data.Top + i)).Take(MaxAnchors)),
            };
            return ToolResult.Changed(content, change, $"중복 {removed}개를 지웠어요, {kept.Count}개가 남았어요");
        }

        private static List<int> ReadColumns(ExcelBook book, ExcelSheet sheet, CellRange range, ToolInput input, bool hasHeader)
        {
            if (!input.Has("columns"))
                return Enumerable.Range(range.Left, range.ColumnCount).ToList();

            var names = input.GetStringList("columns");
            if (names.Count == 0)
                return Enumerable.Range(range.Left, range.ColumnCount).ToList();

            var columns = new List<int>();
            foreach (var name in names)
            {
                var column = RangeColumns.Resolve(book, sheet, range, hasHeader, name, "columns", "중복을 가릴 열은");
                if (!columns.Contains(column))
                    columns.Add(column);
            }

            return columns.OrderBy(c => c).ToList();
        }

        private static string Describe(IReadOnlyList<int> columns) =>
            columns.Count == 1 ? "column " + CellAddress.ColumnName(columns[0]) : "columns " + string.Join(", ", columns.Select(CellAddress.ColumnName));

        private static string KeyOf(ExcelBook book, ExcelSheet sheet, IReadOnlyList<int> columns, int row)
        {
            var parts = new string[columns.Count];
            for (var i = 0; i < columns.Count; i++)
            {
                var cell = sheet.FindCell(columns[i], row);
                if (cell != null && cell.CellFormula != null && cell.CellValue == null)
                    return "\u0000" + row.ToString(System.Globalization.CultureInfo.InvariantCulture);

                parts[i] = cell == null ? string.Empty : book.DisplayText(book.ReadValue(cell), book.StyleOf(cell));
            }

            return string.Join("\u001f", parts);
        }

        private static void CheckOutside(ExcelBook book, ExcelSheet sheet, CellRange data)
        {
            foreach (var other in book.Sheets)
            {
                foreach (var entry in other.Entries)
                {
                    if (entry.Cell.CellFormula == null || (other == sheet && data.Contains(entry.Column, entry.Row)))
                        continue;

                    var formula = other.FormulaOf(entry.Cell, entry.Column, entry.Row);
                    if (string.IsNullOrEmpty(formula))
                        continue;

                    foreach (var reference in FormulaText.References(formula))
                    {
                        if (!reference.IsOnSheet(sheet.Name, other.Name))
                            continue;

                        var full = reference.ToFullRange();
                        if (!full.Intersects(data) || (full.Top <= data.Top && full.Bottom >= data.Bottom))
                            continue;

                        var where = other == sheet ? entry.Address.ToString() : $"‘{other.Name}’ 시트의 {entry.Address}";
                        throw new ToolInputException($"{where} 칸의 수식(={formula})이 중복 제거 범위의 일부 줄만 가리키고 있어서, 줄이 위로 올라오면 엉뚱한 값을 가리켜요. 중복을 지우지 않았어요. 범위를 그 수식까지 넓히거나 수식을 고친 뒤 다시 해 주세요.");
                    }
                }
            }
        }
    }
}
