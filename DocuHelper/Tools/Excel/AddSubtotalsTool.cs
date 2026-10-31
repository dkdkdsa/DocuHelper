using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class AddSubtotalsTool : ExcelTool
    {
        private const int MaxGroups = 500;
        private const string SubtotalSuffix = " 소계";
        private const string GrandTotalLabel = "총합계";

        private static readonly Dictionary<string, int> s_functions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["sum"] = 9,
            ["count"] = 3,
            ["count_numbers"] = 2,
            ["average"] = 1,
            ["max"] = 4,
            ["min"] = 5,
            ["product"] = 6,
        };

        public override string Name => "add_subtotals";

        public override string Description =>
            "Add subtotals like Excel's 부분합. Give range (the table including its header row), group_by (the column whose value defines the groups, by header text or letter) "
            + "and columns (the numeric columns to total). Each run of equal group values gets a row labelled \"값 소계\" with SUBTOTAL formulas; the detail rows above it are grouped at outline level 1, "
            + "and a 총합계 row with SUBTOTAL over the whole list is added at the bottom. function is sum (default), count, count_numbers, average, max, min or product. "
            + "The group column must be sorted: a value that appears in two separate places is refused and the rows where it breaks are named. "
            + "Refused when the range already has subtotals. action remove deletes the subtotal and grand total rows and clears the grouping.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "range": {"type": "string", "description": "Table range including the header row, e.g. \"A1:E40\"."},
                "group_by": {"type": "string", "description": "Header text or column letter whose value defines the groups, e.g. \"부서\" or \"B\"."},
                "columns": {"type": "array", "items": {"type": "string"}, "description": "Header texts or column letters to total, e.g. [\"매출\", \"수량\"]."},
                "function": {"type": "string", "enum": ["sum", "count", "count_numbers", "average", "max", "min", "product"], "description": "Summary function. Default sum."},
                "action": {"type": "string", "enum": ["add", "remove"], "description": "add (default) or remove the subtotal rows."},
                "has_header": {"type": "boolean", "description": "Whether the first row of range is a header. Default true."}
              },
              "required": ["sheet", "range", "group_by"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, null, true);
            var range = ParseRange(sheet, input.GetString("range"));
            var action = (input.GetOptionalString("action") ?? "add").Trim().ToLowerInvariant();
            return action switch
            {
                "add" => Add(docu, book, sheet, range, input),
                "remove" => Remove(docu, book, sheet, range),
                _ => throw new ToolInputException("action은 add나 remove 중 하나로 알려 주세요."),
            };
        }

        private ToolResult Add(ExcelDocu docu, ExcelBook book, ExcelSheet sheet, CellRange range, ToolInput input)
        {
            if (range.CellCount > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 부분합을 넣을 수 있어요.");

            var hasHeader = input.GetOptionalBool("has_header") ?? true;
            var first = hasHeader ? range.Top + 1 : range.Top;
            if (first > range.Bottom)
                throw new ToolInputException("부분합을 넣을 데이터 행이 없어요.");

            var function = FunctionNumber(input.GetOptionalString("function"));
            var group = ResolveColumn(book, sheet, range, hasHeader, input.GetString("group_by"));
            var columns = input.GetStringList("columns").Select(text => ResolveColumn(book, sheet, range, hasHeader, text)).Distinct().ToList();
            if (columns.Count == 0)
                throw new ToolInputException("합계를 낼 열을 columns로 알려 주세요.");

            if (columns.Contains(group))
                throw new ToolInputException("그룹 열은 합계 열로 고를 수 없어요.");

            for (var row = range.Top; row <= range.Bottom; row++)
            {
                for (var column = range.Left; column <= range.Right; column++)
                {
                    if (sheet.ContainsSubtotal(sheet.FindCell(column, row)))
                        throw new ToolInputException($"이 범위에 이미 부분합이 있어요({CellAddress.ColumnName(column)}{row + 1} 칸). 먼저 action을 remove로 지워 주세요.");
                }
            }

            var groups = GroupRuns(book, sheet, group, first, range.Bottom);
            var bottom = range.Bottom;
            var grandRow = bottom + 1 + groups.Count;
            var dirty = new List<CellKey>();
            InsertLine(docu, book, sheet, bottom + 1, dirty);
            for (var i = groups.Count - 1; i >= 0; i--)
                InsertLine(docu, book, sheet, groups[i].End + 1, dirty);

            sheet.PrepareForWrite();
            var written = new List<CellKey>();
            var addresses = new List<CellAddress>();
            var formulas = new List<CellKey>();
            for (var i = 0; i < groups.Count; i++)
            {
                var detailFirst = groups[i].Start + i;
                var detailLast = groups[i].End + i;
                var subtotalRow = detailLast + 1;
                for (var row = detailFirst; row <= detailLast; row++)
                    sheet.SetRowOutline(row, 1);

                WriteText(book, sheet, group, subtotalRow, groups[i].Label + SubtotalSuffix, written, addresses);
                foreach (var column in columns)
                {
                    var letter = CellAddress.ColumnName(column);
                    var formula = $"SUBTOTAL({function},{letter}{detailFirst + 1}:{letter}{detailLast + 1})";
                    WriteFormula(book, sheet, column, subtotalRow, formula, written, addresses, formulas);
                }
            }

            WriteText(book, sheet, group, grandRow, GrandTotalLabel, written, addresses);
            foreach (var column in columns)
            {
                var letter = CellAddress.ColumnName(column);
                WriteFormula(book, sheet, column, grandRow, $"SUBTOTAL({function},{letter}{first + 1}:{letter}{grandRow})", written, addresses, formulas);
            }

            sheet.SyncOutlineLevels();
            sheet.UpdateDimension();
            var result = Recalculate(book, written.Concat(dirty), false);
            sheet.Invalidate();
            var span = new CellRange(range.Left, range.Top, range.Right, grandRow);
            var content = $"Added {groups.Count} subtotal row(s) to ‘{sheet.Name}’ {span}, grouped the detail rows and put the grand total at row {grandRow + 1}."
                + FormulaReport(result, formulas, 12);
            var change = new DocuChange
            {
                Location = Place(span),
                Icon = "sigma",
                Anchors = Anchors(sheet, addresses),
            };
            return ToolResult.Changed(content, change, $"{groups.Count}그룹 소계");
        }

        private ToolResult Remove(ExcelDocu docu, ExcelBook book, ExcelSheet sheet, CellRange range)
        {
            if (range.CellCount > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 지울 수 있어요.");

            var subtotalRows = Enumerable.Range(range.Top, range.RowCount)
                .Where(row => Enumerable.Range(range.Left, range.ColumnCount).Any(column => sheet.ContainsSubtotal(sheet.FindCell(column, row))))
                .ToList();
            if (subtotalRows.Count == 0)
                throw new ToolInputException("이 범위에 지울 부분합이 없어요.");

            sheet.PrepareForWrite();
            var dirty = new List<CellKey>();
            for (var i = subtotalRows.Count - 1; i >= 0; i--)
            {
                var shift = new LineShift(true, subtotalRows[i], -1);
                var outcome = ExcelStructure.ShiftLines(book, sheet, shift, ExcelStructure.FormatNone);
                ShiftAnchors(docu, sheet.Name, shift);
                dirty.AddRange(outcome.DirtyKeys(book));
            }

            var bottom = range.Bottom - subtotalRows.Count;
            for (var row = range.Top; row <= bottom; row++)
                sheet.SetRowOutline(row, 0);

            sheet.SyncOutlineLevels();
            sheet.UpdateDimension();
            var result = Recalculate(book, dirty, false);
            sheet.Invalidate();
            var content = $"Removed {subtotalRows.Count} subtotal row(s) from ‘{sheet.Name}’ and cleared the grouping of {new CellRange(range.Left, range.Top, range.Right, Math.Max(range.Top, bottom))}."
                + (result.Updated > 0 ? $" {result.Updated} formula cell(s) were recalculated." : string.Empty);
            var addresses = Enumerable.Range(range.Top, Math.Max(0, bottom - range.Top + 1)).Select(row => new CellAddress(range.Left, row));
            var change = new DocuChange
            {
                Location = Place(new CellRange(range.Left, range.Top, range.Right, Math.Max(range.Top, bottom))),
                Icon = "trash",
                Anchors = Anchors(sheet, addresses),
            };
            return ToolResult.Changed(content, change, $"{subtotalRows.Count}행 지움");
        }

        private static List<Group> GroupRuns(ExcelBook book, ExcelSheet sheet, int column, int first, int last)
        {
            var groups = new List<Group>();
            var firstRows = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var row = first; row <= last; row++)
            {
                var label = DisplayAt(book, sheet, column, row).Trim();
                if (label.Length == 0)
                    throw new ToolInputException($"그룹 열 {CellAddress.ColumnName(column)}{row + 1} 칸이 비어 있어요. 그룹 열은 모든 행에 값이 있어야 해요.");

                if (groups.Count > 0 && groups[^1].Label == label)
                {
                    groups[^1] = groups[^1] with { End = row };
                    continue;
                }

                if (firstRows.TryGetValue(label, out var earlier))
                    throw new ToolInputException($"‘{label}’ 값이 {earlier + 1}행과 {row + 1}행에 떨어져 있어요. 그룹 열을 정렬해서 같은 값이 한 덩어리로 모이게 한 뒤 다시 해 주세요.");

                firstRows[label] = row;
                groups.Add(new Group(label, row, row));
                if (groups.Count > MaxGroups)
                    throw new ToolInputException($"그룹이 {MaxGroups}개를 넘어요. 그룹 수를 줄여 주세요.");
            }

            return groups;
        }

        private static int FunctionNumber(string text)
        {
            var key = string.IsNullOrWhiteSpace(text) ? "sum" : text.Trim();
            return s_functions.TryGetValue(key, out var number)
                ? number
                : throw new ToolInputException($"function은 sum, count, count_numbers, average, max, min, product 중 하나로 알려 주세요. 받은 값: {key}");
        }

        private static void WriteText(ExcelBook book, ExcelSheet sheet, int column, int row, string text, List<CellKey> written, List<CellAddress> addresses)
        {
            var cell = sheet.GetOrCreateCell(column, row);
            book.SetText(cell, text);
            Emphasize(book, cell);
            written.Add(new CellKey(sheet.Index, column, row));
            addresses.Add(new CellAddress(column, row));
        }

        private static void WriteFormula(ExcelBook book, ExcelSheet sheet, int column, int row, string formula, List<CellKey> written, List<CellAddress> addresses, List<CellKey> formulas)
        {
            var cell = sheet.GetOrCreateCell(column, row);
            ExcelBook.SetFormula(cell, formula);
            Emphasize(book, cell);
            var key = new CellKey(sheet.Index, column, row);
            written.Add(key);
            formulas.Add(key);
            addresses.Add(new CellAddress(column, row));
        }

        private sealed record Group(string Label, int Start, int End);
    }
}
