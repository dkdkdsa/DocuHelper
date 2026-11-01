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
        private const int MaxOutlineLines = 5000;

        public override string Name => "format_rows_columns";

        public override string Description =>
            "Change the width, height or visibility of whole rows or columns. Pass rows (\"2\" or \"2:5\") or columns (\"B\" or \"B:D\"), not both. "
            + "width (columns only) is in Excel character units like Excel's 열 너비 (default about 8.4), or \"auto\" to fit the longest value. "
            + "height (rows only) is in points like Excel's 행 높이 (default about 16.5), or \"auto\" to let Excel size the row to its content. "
            + "hidden true hides them like Excel's 숨기기 (the data stays), false shows them again. "
            + "outline works like Excel's 개요: group adds one level to the lines, ungroup removes one level, collapse hides the grouped lines and marks the summary line, expand shows them again, clear removes the outline. "
            + "outline cannot be combined with width, height or hidden. summary_below and summary_right set where the summary row or column sits like Excel's 요약 행/열 위치; they can be passed alone without rows or columns.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "rows": {"type": "string", "description": "Rows, e.g. \"2\" or \"2:5\"."},
                "columns": {"type": "string", "description": "Columns, e.g. \"B\" or \"B:D\"."},
                "width": {"type": ["number", "string"], "description": "Column width in characters (0-255), or \"auto\"."},
                "height": {"type": ["number", "string"], "description": "Row height in points (0-409), or \"auto\"."},
                "hidden": {"type": "boolean", "description": "true to hide, false to show again."},
                "outline": {"type": "string", "enum": ["group", "ungroup", "collapse", "expand", "clear"], "description": "Outline action for the rows or columns, like Excel's 그룹, 그룹 해제, 축소, 확장 and 개요 지우기."},
                "summary_below": {"type": "boolean", "description": "true puts the summary row below its group (Excel's default), false above."},
                "summary_right": {"type": "boolean", "description": "true puts the summary column right of its group (Excel's default), false left."}
              },
              "required": ["sheet"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, null, true);
            var summaryBelow = input.GetOptionalBool("summary_below");
            var summaryRight = input.GetOptionalBool("summary_right");
            var summary = summaryBelow.HasValue || summaryRight.HasValue;
            var outline = OutlineAction(input);
            var width = Size(input, "width", MaxWidth);
            var height = Size(input, "height", MaxHeight);
            var hidden = input.GetOptionalBool("hidden");

            if (!HasLines(input))
            {
                if (!summary)
                    throw new ToolInputException("rows와 columns 중 하나만 알려 주세요. 예: rows \"5:7\" 또는 columns \"C\".");

                if (outline != null || width.Given || height.Given || hidden.HasValue)
                    throw new ToolInputException("너비, 높이, 숨기기, 개요 작업은 행이나 열을 알려 주세요. 예: rows \"5:7\" 또는 columns \"C\".");

                sheet.SetSummary(summaryBelow, summaryRight);
                sheet.SyncOutlineLevels();
                sheet.Invalidate();
                var position = SummaryText(summaryBelow, summaryRight);
                var summaryChange = new DocuChange
                {
                    Location = sheet.Name,
                    Icon = "resize",
                    Anchors = new List<string>(),
                };
                return ToolResult.Changed($"‘{sheet.Name}’ outline summary: {position}.", summaryChange, "요약 위치");
            }

            var (rows, first, last) = ParseLines(input);
            if (rows && width.Given)
                throw new ToolInputException("width는 열에만 쓸 수 있어요. 행은 height로 알려 주세요.");

            if (!rows && height.Given)
                throw new ToolInputException("height는 행에만 쓸 수 있어요. 열은 width로 알려 주세요.");

            if (outline != null && (width.Given || height.Given || hidden.HasValue))
                throw new ToolInputException("개요 작업은 너비, 높이, 숨기기와 함께 쓸 수 없어요. 나눠서 알려 주세요.");

            if (!width.Given && !height.Given && !hidden.HasValue && outline == null && !summary)
                throw new ToolInputException("바꿀 것을 알려 주세요. (width, height, hidden, outline, summary_below, summary_right)");

            if (rows && last - first >= 100000)
                throw new ToolInputException("한 번에 10만 행까지만 바꿀 수 있어요.");

            if (outline != null && last - first >= MaxOutlineLines)
                throw new ToolInputException($"개요 작업은 한 번에 {MaxOutlineLines}개까지만 할 수 있어요.");

            sheet.PrepareForWrite();
            var parts = new List<string>();
            if (summary)
            {
                sheet.SetSummary(summaryBelow, summaryRight);
                parts.Add("set the summary position to " + SummaryText(summaryBelow, summaryRight));
            }

            if (outline != null)
                parts.Add(ApplyOutline(sheet, outline, rows, first, last));

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

            sheet.SyncOutlineLevels();
            sheet.Invalidate();
            var content = $"‘{sheet.Name}’ {LineText(rows, first, last)}: {string.Join(", ", parts)}.";
            var change = new DocuChange
            {
                Location = LineLabel(rows, first, last),
                Icon = outline == "collapse" || hidden == true ? "eye-off" : hidden == false || outline == "expand" ? "eye" : "resize",
                Anchors = hidden == true || outline == "collapse" ? new List<string>() : LineAnchors(sheet, rows, first, last),
            };
            var detail = outline != null ? "개요" : hidden == true ? "숨김" : hidden == false ? "다시 보임" : rows ? "높이" : "너비";
            return ToolResult.Changed(content, change, detail);
        }

        private static bool HasLines(ToolInput input) =>
            !string.IsNullOrWhiteSpace(input.GetOptionalString("rows")) || !string.IsNullOrWhiteSpace(input.GetOptionalString("columns"));

        private static string OutlineAction(ToolInput input)
        {
            var text = input.GetOptionalString("outline")?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(text))
                return null;

            return text is "group" or "ungroup" or "collapse" or "expand" or "clear"
                ? text
                : throw new ToolInputException($"outline은 group, ungroup, collapse, expand, clear 중 하나로 알려 주세요. 받은 값: {text}");
        }

        private static string SummaryText(bool? below, bool? right)
        {
            var parts = new List<string>();
            if (below.HasValue)
                parts.Add(below.Value ? "below the group" : "above the group");

            if (right.HasValue)
                parts.Add(right.Value ? "right of the group" : "left of the group");

            return string.Join(", ", parts);
        }

        private static string ApplyOutline(ExcelSheet sheet, string action, bool rows, int first, int last)
        {
            var summary = rows ? (sheet.SummaryBelow ? last + 1 : first - 1) : (sheet.SummaryRight ? last + 1 : first - 1);
            var summaryValid = summary >= 0 && summary < (rows ? CellAddress.MaxRows : CellAddress.MaxColumns);
            switch (action)
            {
                case "group":
                    for (var line = first; line <= last; line++)
                        SetLevel(sheet, rows, line, Math.Min(ExcelSheet.MaxOutlineLevel, LevelOf(sheet, rows, line) + 1));

                    return "grouped them";
                case "ungroup":
                    for (var line = first; line <= last; line++)
                    {
                        var before = LevelOf(sheet, rows, line);
                        var level = Math.Max(0, before - 1);
                        SetLevel(sheet, rows, line, level);
                        if (before > 0 && level == 0)
                        {
                            SetHidden(sheet, rows, line, false);
                            SetCollapsed(sheet, rows, line, false);
                        }
                    }

                    return "ungrouped them";
                case "clear":
                    for (var line = first; line <= last; line++)
                    {
                        if (LevelOf(sheet, rows, line) > 0)
                            SetHidden(sheet, rows, line, false);

                        SetLevel(sheet, rows, line, 0);
                        SetCollapsed(sheet, rows, line, false);
                    }

                    return "cleared the outline";
                case "collapse":
                    if (!Enumerable.Range(first, last - first + 1).Any(line => LevelOf(sheet, rows, line) > 0))
                        throw new ToolInputException("접을 그룹이 없어요. 먼저 outline을 group으로 묶어 주세요.");

                    for (var line = first; line <= last; line++)
                    {
                        if (LevelOf(sheet, rows, line) > 0)
                            SetHidden(sheet, rows, line, true);
                    }

                    if (summaryValid)
                        SetCollapsed(sheet, rows, summary, true);

                    return "collapsed the group";
                case "expand":
                    for (var line = first; line <= last; line++)
                    {
                        if (LevelOf(sheet, rows, line) > 0)
                            SetHidden(sheet, rows, line, false);
                    }

                    if (summaryValid)
                        SetCollapsed(sheet, rows, summary, false);

                    return "expanded the group";
                default:
                    throw new ToolInputException($"알 수 없는 outline 작업이에요: {action}");
            }
        }

        private static int LevelOf(ExcelSheet sheet, bool rows, int line) =>
            rows ? sheet.RowOutline(line) : sheet.ColumnOutline(line);

        private static void SetLevel(ExcelSheet sheet, bool rows, int line, int level)
        {
            if (rows)
                sheet.SetRowOutline(line, level);
            else
                sheet.SetColumnOutline(line, level);
        }

        private static void SetHidden(ExcelSheet sheet, bool rows, int line, bool hidden)
        {
            if (rows)
                sheet.SetRowHidden(line, hidden);
            else
                sheet.SetColumnHidden(line, line, hidden);
        }

        private static void SetCollapsed(ExcelSheet sheet, bool rows, int line, bool collapsed)
        {
            if (rows)
                sheet.SetRowCollapsed(line, collapsed);
            else
                sheet.SetColumnCollapsed(line, collapsed);
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
