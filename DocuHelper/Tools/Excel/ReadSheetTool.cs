using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class ReadSheetTool : ExcelTool
    {
        private const int MaxRows = 200;
        private const int MaxChars = 8000;
        private const int MaxCellChars = 120;

        public override string Name => "read_sheet";

        public override string Description =>
            "Read cells from one sheet of an Excel workbook. The answer starts with every sheet and its used range, then lists the rows of the requested range as "
            + "`row | ref: value | ref: value`, skipping empty cells. Values are shown the way Excel displays them (dates as 2026-06-02, 1,234, 12%); a formula cell "
            + "also shows its formula, e.g. `F2: 480,000 (=D2*E2)`. Text that only looks like a number starts with '. "
            + "At most 200 rows or about 8000 characters come back per call; use offset to read further.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name. Defaults to the first sheet."},
                "range": {"type": "string", "description": "A1 range to read, e.g. \"A1:F20\" or \"B:D\". Defaults to the sheet's used range."},
                "offset": {"type": "integer", "description": "Rows of the range to skip, for paging. Default 0."},
                "limit": {"type": "integer", "description": "Maximum rows to return, 1-200. Default 200."}
              }
            }
            """;

        public override bool IsReadOnly => true;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", false);
            var used = sheet.UsedRange;
            var rangeText = input.GetOptionalString("range");
            var range = string.IsNullOrWhiteSpace(rangeText) ? used : ParseRange(sheet, rangeText);
            var offset = Math.Max(0, input.GetOptionalInt("offset") ?? 0);
            var limit = Math.Clamp(input.GetOptionalInt("limit") ?? MaxRows, 1, MaxRows);

            var text = new StringBuilder();
            text.Append("Sheets: ").AppendLine(string.Join(", ", book.Sheets.Select(SheetSummary)));
            if (range == null)
            {
                text.Append($"‘{sheet.Name}’ is empty.");
                return ToolResult.Ok(text.ToString(), "빈 시트");
            }

            var first = range.Top + offset;
            if (first > range.Bottom)
            {
                text.Append($"‘{sheet.Name}’ {range} has only {range.RowCount} row(s); offset {offset} is past the end.");
                return ToolResult.Ok(text.ToString(), "0줄");
            }

            var last = (int)Math.Min(range.Bottom, (long)first + limit - 1);
            text.AppendLine($"‘{sheet.Name}’ {range}, rows {first + 1}-{last + 1}:");

            var rows = sheet.Entries
                .Where(e => e.Row >= first && e.Row <= last && e.Column >= range.Left && e.Column <= range.Right)
                .GroupBy(e => e.Row)
                .OrderBy(g => g.Key);

            var printed = 0;
            int? stoppedAt = null;
            foreach (var row in rows)
            {
                var cells = row.OrderBy(e => e.Column)
                    .Select(e => (e.Address, Text: CellText(book, sheet, e)))
                    .Where(c => c.Text.Length > 0)
                    .Select(c => c.Address + ": " + c.Text)
                    .ToList();

                if (cells.Count == 0)
                    continue;

                var line = (row.Key + 1) + " | " + string.Join(" | ", cells);
                if (printed > 0 && text.Length + line.Length > MaxChars)
                {
                    stoppedAt = row.Key;
                    break;
                }

                text.AppendLine(line);
                printed++;
            }

            if (printed == 0)
                text.AppendLine("(no values in these rows)");

            if (stoppedAt.HasValue)
                text.AppendLine($"Stopped before row {stoppedAt.Value + 1} to keep this short. Call read_sheet again with offset={stoppedAt.Value - range.Top} for the rest.");
            else if (last < range.Bottom)
                text.AppendLine($"Rows {last + 2}-{range.Bottom + 1} not shown. Call read_sheet again with offset={last + 1 - range.Top} for them.");

            var merged = sheet.MergedRanges().Where(m => m.Intersects(range)).Take(10).ToList();
            if (merged.Count > 0)
                text.AppendLine("Merged cells: " + string.Join(", ", merged));

            return ToolResult.Ok(text.ToString().TrimEnd(), $"{printed}줄");
        }

        private static string SheetSummary(ExcelSheet sheet)
        {
            var used = sheet.UsedRange;
            var text = used == null ? $"‘{sheet.Name}’ (empty)" : $"‘{sheet.Name}’ {used}";
            return sheet.IsHidden ? text + " (hidden)" : text;
        }

        private static string CellText(ExcelBook book, ExcelSheet sheet, ExcelCellEntry entry)
        {
            var cell = entry.Cell;
            var formula = sheet.FormulaOf(cell, entry.Column, entry.Row);
            var value = book.ReadValue(cell);
            string shown;
            if (formula != null && cell.CellValue == null)
                shown = "(not calculated)";
            else
                shown = Display(value, book.StyleOf(cell));

            if (formula != null)
                shown = (shown.Length > 0 ? shown + " " : string.Empty) + "(=" + formula + ")";

            return shown;
        }

        private static string Display(ExcelValue value, ExcelCellStyle style)
        {
            switch (value.Kind)
            {
                case ExcelValueKind.Number:
                    var display = ExcelNumberFormat.Format(value.Number, style.FormatCode);
                    if (!TypedInput.TryParseNumber(display, out var parsed, out _) || Math.Abs(parsed - value.Number) > 1e-9 * Math.Max(1, Math.Abs(value.Number)))
                        display += " [" + ExcelValue.Invariant(value.Number) + "]";

                    return display;
                case ExcelValueKind.Text:
                    var text = value.Text;
                    if (text.Length == 0)
                        return string.Empty;

                    if (TypedInput.Parse(text).Kind != TypedInputKind.Text)
                        text = "'" + text;

                    text = text.Replace("\r", string.Empty).Replace("\n", "\\n");
                    return text.Length > MaxCellChars ? text.Substring(0, MaxCellChars) + "…" : text;
                case ExcelValueKind.Boolean:
                    return value.Boolean ? "TRUE" : "FALSE";
                case ExcelValueKind.Error:
                    return value.Text;
                default:
                    return string.Empty;
            }
        }
    }
}
