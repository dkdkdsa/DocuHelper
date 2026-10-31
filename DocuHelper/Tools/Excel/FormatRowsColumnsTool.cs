using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class FormatRowsColumnsTool : ExcelTool
    {
        private const double MaxWidth = 255;
        private const double MaxHeight = 409;

        public override string Name => "format_rows_columns";

        public override string Description =>
            "Change the width, height or visibility of whole rows or columns. Pass rows (\"2\" or \"2:5\") or columns (\"B\" or \"B:D\"), not both. "
            + "width (columns only) is in Excel character units like Excel's 열 너비 (default about 8.4), or \"auto\" to fit the longest value. "
            + "height (rows only) is in points like Excel's 행 높이 (default about 16.5), or \"auto\" to let Excel size the row to its content. "
            + "hidden true hides them like Excel's 숨기기 (the data stays), false shows them again.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "rows": {"type": "string", "description": "Rows, e.g. \"2\" or \"2:5\"."},
                "columns": {"type": "string", "description": "Columns, e.g. \"B\" or \"B:D\"."},
                "width": {"type": ["number", "string"], "description": "Column width in characters (0-255), or \"auto\"."},
                "height": {"type": ["number", "string"], "description": "Row height in points (0-409), or \"auto\"."},
                "hidden": {"type": "boolean", "description": "true to hide, false to show again."}
              },
              "required": ["sheet"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, null, true);
            var (rows, first, last) = ParseLines(input);
            var width = Size(input, "width", MaxWidth);
            var height = Size(input, "height", MaxHeight);
            var hidden = input.GetOptionalBool("hidden");
            if (rows && width.Given)
                throw new ToolInputException("width는 열에만 쓸 수 있어요. 행은 height로 알려 주세요.");

            if (!rows && height.Given)
                throw new ToolInputException("height는 행에만 쓸 수 있어요. 열은 width로 알려 주세요.");

            if (!width.Given && !height.Given && !hidden.HasValue)
                throw new ToolInputException("바꿀 것을 알려 주세요. (width, height, hidden)");

            if (rows && last - first >= 100000)
                throw new ToolInputException("한 번에 10만 행까지만 바꿀 수 있어요.");

            sheet.PrepareForWrite();
            var parts = new List<string>();
            if (width.Given)
            {
                if (width.Auto)
                {
                    var fitted = new List<string>();
                    for (var column = first; column <= last; column++)
                    {
                        var fit = FitWidth(book, sheet, column);
                        sheet.SetColumnWidth(column, column, fit);
                        fitted.Add($"{CellAddress.ColumnName(column)} {fit.ToString("0.#", CultureInfo.InvariantCulture)}");
                    }

                    parts.Add("fitted the width to the content (" + string.Join(", ", fitted.Take(12)) + (fitted.Count > 12 ? ", …" : string.Empty) + ")");
                }
                else
                {
                    sheet.SetColumnWidth(first, last, width.Value);
                    parts.Add($"set the width to {width.Value.ToString("0.##", CultureInfo.InvariantCulture)}");
                }
            }

            if (height.Given)
            {
                for (var row = first; row <= last; row++)
                    sheet.SetRowHeight(row, height.Auto ? null : height.Value);

                parts.Add(height.Auto ? "let Excel size the height to the content" : $"set the height to {height.Value.ToString("0.##", CultureInfo.InvariantCulture)} pt");
            }

            if (hidden.HasValue)
            {
                if (rows)
                {
                    for (var row = first; row <= last; row++)
                        sheet.SetRowHidden(row, hidden.Value);
                }
                else
                {
                    sheet.SetColumnHidden(first, last, hidden.Value);
                }

                parts.Add(hidden.Value ? "hid them" : "showed them again");
            }

            sheet.Invalidate();
            var content = $"‘{sheet.Name}’ {LineText(rows, first, last)}: {string.Join(", ", parts)}.";
            var change = new DocuChange
            {
                Location = LineLabel(rows, first, last),
                Icon = hidden == true ? "eye-off" : hidden == false ? "eye" : "resize",
                Anchors = hidden == true ? new List<string>() : LineAnchors(sheet, rows, first, last),
            };
            var detail = hidden == true ? "숨김" : hidden == false ? "다시 보임" : rows ? "높이" : "너비";
            return ToolResult.Changed(content, change, detail);
        }

        private static (bool Given, bool Auto, double Value) Size(ToolInput input, string field, double max)
        {
            if (!input.Has(field))
                return (false, false, 0);

            var element = input.Get(field);
            if (element.ValueKind == JsonValueKind.Null)
                return (false, false, 0);

            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString()?.Trim() ?? string.Empty;
                if (string.Equals(text, "auto", StringComparison.OrdinalIgnoreCase))
                    return (true, true, 0);

                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    throw new ToolInputException($"{field} 값은 숫자나 \"auto\"로 알려 주세요. 받은 값: {text}");

                element = JsonSerializer.SerializeToElement(parsed);
            }

            if (element.ValueKind != JsonValueKind.Number)
                throw new ToolInputException($"{field} 값은 숫자나 \"auto\"로 알려 주세요.");

            var value = element.GetDouble();
            if (double.IsNaN(value) || value < 0 || value > max)
                throw new ToolInputException($"{field} 값은 0부터 {max} 사이여야 해요.");

            return (true, false, Math.Round(value, 2));
        }

        private static double FitWidth(ExcelBook book, ExcelSheet sheet, int column)
        {
            var widest = 0.0;
            var merged = sheet.MergedRanges().Where(m => m.ColumnCount > 1).ToList();
            foreach (var entry in sheet.Entries)
            {
                if (entry.Column != column || !ExcelSheet.HasContent(entry.Cell) || merged.Any(m => m.Contains(entry.Column, entry.Row)))
                    continue;

                var style = book.StyleOf(entry.Cell);
                var text = book.DisplayText(book.ReadValue(entry.Cell), style);
                if (style.Wrap)
                    text = text.Split('\n').OrderByDescending(l => l.Length).FirstOrDefault() ?? string.Empty;

                var scale = (style.FontSize ?? 11) / 11 * (style.Bold ? 1.08 : 1);
                widest = Math.Max(widest, text.Sum(c => c >= 0x2E80 ? 1.8 : 1.0) * scale + 1.5);
            }

            var minimum = sheet.DefaultColumnWidth ?? 8.43;
            return Math.Min(MaxWidth, Math.Ceiling(Math.Max(widest, minimum) * 2) / 2);
        }
    }
}
