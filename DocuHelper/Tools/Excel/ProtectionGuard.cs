using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Tools;

namespace DocuHelper.Tools.Excel
{
    public static class ProtectionGuard
    {
        private const int MaxGuardedCells = 20000;

        private static readonly SheetAction[] s_sheetActions =
        {
            new("select_locked", "잠긴 셀 선택", false, p => p.SelectLockedCells, (p, v) => p.SelectLockedCells = v),
            new("select_unlocked", "잠금 해제된 셀 선택", false, p => p.SelectUnlockedCells, (p, v) => p.SelectUnlockedCells = v),
            new("format_cells", "셀 서식", true, p => p.FormatCells, (p, v) => p.FormatCells = v),
            new("format_columns", "열 서식", true, p => p.FormatColumns, (p, v) => p.FormatColumns = v),
            new("format_rows", "행 서식", true, p => p.FormatRows, (p, v) => p.FormatRows = v),
            new("insert_columns", "열 삽입", true, p => p.InsertColumns, (p, v) => p.InsertColumns = v),
            new("insert_rows", "행 삽입", true, p => p.InsertRows, (p, v) => p.InsertRows = v),
            new("insert_hyperlinks", "하이퍼링크 삽입", true, p => p.InsertHyperlinks, (p, v) => p.InsertHyperlinks = v),
            new("delete_columns", "열 삭제", true, p => p.DeleteColumns, (p, v) => p.DeleteColumns = v),
            new("delete_rows", "행 삭제", true, p => p.DeleteRows, (p, v) => p.DeleteRows = v),
            new("sort", "정렬", true, p => p.Sort, (p, v) => p.Sort = v),
            new("autofilter", "자동 필터", true, p => p.AutoFilter, (p, v) => p.AutoFilter = v),
            new("pivot", "피벗 테이블", true, p => p.PivotTables, (p, v) => p.PivotTables = v),
            new("objects", "개체 편집", false, p => p.Objects, (p, v) => p.Objects = v),
            new("scenarios", "시나리오 편집", false, p => p.Scenarios, (p, v) => p.Scenarios = v),
        };

        public static IReadOnlyList<string> SheetActionNames { get; } = s_sheetActions.Select(a => a.Name).ToList();

        public static void Check(ExcelBook book, string toolName, ToolInput input)
        {
            if (toolName == "protect")
                return;

            if (!book.Sheets.Any(s => ProtectionOf(s) != null) && !StructureLocked(book))
                return;

            var plan = PlanOf(book, toolName, input);
            if (plan == null)
            {
                if (book.Sheets.Any(s => ProtectionOf(s) != null) || StructureLocked(book))
                    throw new ToolInputException($"‘{toolName}’ 도구는 보호된 통합 문서에서 안전하게 확인할 수 없어서 쓸 수 없어요. 검토 > 시트 보호 해제로 풀어 주세요.");

                return;
            }

            if (plan.Structure && StructureLocked(book))
                throw new ToolInputException("통합 문서 구조가 보호되어 있어서 시트를 추가, 삭제, 이름 바꾸기, 숨기기를 할 수 없어요. 검토 > 통합 문서 보호 해제로 풀어 주세요.");

            foreach (var sheet in SheetsFor(book, plan.Sheet))
            {
                var protection = ProtectionOf(sheet);
                if (protection == null)
                    continue;

                if (plan.LockEdit)
                    throw new ToolInputException("보호된 시트에서는 셀 잠금 상태를 바꿀 수 없어요. 검토 > 시트 보호 해제로 풀어 주세요.");

                if (plan.Blocked.Count > 0)
                    throw new ToolInputException($"‘{sheet.Name}’ 시트는 보호되어 있어서 그 작업을 할 수 없어요. {plan.Blocked[0]} 검토 > 시트 보호 해제로 풀어 주세요.");

                foreach (var name in plan.Actions)
                {
                    if (IsLocked(protection, name))
                        throw new ToolInputException($"‘{sheet.Name}’ 시트는 보호되어 있어서 {LabelOf(name)} 작업을 할 수 없어요. 검토 > 시트 보호 해제로 풀어 주세요.");
                }
            }

            foreach (var target in plan.Cells)
            {
                foreach (var sheet in SheetsFor(book, target.Sheet))
                {
                    if (ProtectionOf(sheet) != null)
                        RequireEditable(book, sheet, target.Range);
                }
            }
        }

        public static void Apply(SheetProtection protection, string action, bool locked)
        {
            var definition = Definition(action);
            definition.Set(protection, locked == definition.LockedByDefault ? null : (BooleanValue)locked);
        }

        public static bool IsLocked(SheetProtection protection, string action)
        {
            var definition = Definition(action);
            return definition.Get(protection)?.Value ?? definition.LockedByDefault;
        }

        public static string LabelOf(string action) => Definition(action).Label;

        public static SheetProtection ProtectionOf(ExcelSheet sheet)
        {
            var protection = sheet.Worksheet?.GetFirstChild<SheetProtection>();
            return protection?.Sheet?.Value == true ? protection : null;
        }

        public static bool StructureLocked(ExcelBook book) =>
            book.WorkbookPart?.Workbook?.WorkbookProtection?.LockStructure?.Value == true;

        private static SheetAction Definition(string action) =>
            s_sheetActions.FirstOrDefault(a => a.Name == action)
            ?? throw new ToolInputException($"‘{action}’은 알 수 없는 시트 작업이에요.");

        private static IEnumerable<ExcelSheet> SheetsFor(ExcelBook book, string name)
        {
            if (name == null)
                return book.Sheets;

            var sheet = book.FindSheet(name)
                ?? throw new ToolInputException($"‘{name.Trim()}’ 시트를 찾을 수 없어요. 있는 시트: {book.SheetList()}");
            return new[] { sheet };
        }

        private static void RequireEditable(ExcelBook book, ExcelSheet sheet, CellRange range)
        {
            if (range.CellCount > MaxGuardedCells)
                throw new ToolInputException($"보호된 시트에서는 한 번에 {MaxGuardedCells.ToString(System.Globalization.CultureInfo.InvariantCulture)}칸까지만 확인할 수 있어요. 범위를 나눠 주세요.");

            foreach (var address in range.Cells())
            {
                if (book.Styles.Describe(StyleIndexOf(sheet, address.Column, address.Row)).Locked && !InAllowedRange(sheet, address))
                    throw new ToolInputException($"보호된 시트라서 고칠 수 없어요. ‘{sheet.Name}’의 {address} 칸은 잠겨 있어요. 검토 > 시트 보호 해제로 풀어 주세요.");
            }
        }

        private static uint StyleIndexOf(ExcelSheet sheet, int column, int row)
        {
            var cell = sheet.FindCell(column, row);
            if (cell != null)
                return cell.StyleIndex?.Value ?? 0;

            var line = sheet.FindRow(row);
            if (line?.CustomFormat?.Value == true && line.StyleIndex != null)
                return line.StyleIndex.Value;

            var number = (uint)(column + 1);
            foreach (var definition in sheet.Worksheet?.GetFirstChild<Columns>()?.Elements<Column>() ?? Enumerable.Empty<Column>())
            {
                if (definition.Min?.Value <= number && definition.Max?.Value >= number)
                    return definition.Style?.Value ?? 0;
            }

            return 0;
        }

        private static bool InAllowedRange(ExcelSheet sheet, CellAddress address) =>
            AllowedRanges(sheet).Any(range => range.Contains(address.Column, address.Row));

        private static IEnumerable<CellRange> AllowedRanges(ExcelSheet sheet)
        {
            var ranges = sheet.Worksheet?.GetFirstChild<ProtectedRanges>();
            if (ranges == null)
                yield break;

            foreach (var item in ranges.Elements<ProtectedRange>())
            {
                if (!string.IsNullOrEmpty(item.Password?.Value) || !string.IsNullOrEmpty(item.HashValue?.Value))
                    continue;

                foreach (var part in (item.SequenceOfReferences?.InnerText ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (CellRange.TryParse(part, out var range))
                        yield return range;
                }
            }
        }

        private static PlanResult PlanOf(ExcelBook book, string tool, ToolInput input)
        {
            var plan = new PlanResult { Sheet = input.GetOptionalString("sheet") };
            switch (tool)
            {
                case "write_cells":
                    AddWriteTargets(book, plan, input);
                    break;
                case "set_formula":
                    plan.Cells.Add(TargetOf(book, input.GetString("range"), plan.Sheet, null));
                    break;
                case "format_range":
                    plan.Actions.Add("format_cells");
                    if (input.Has("locked"))
                        plan.LockEdit = true;
                    break;
                case "sort_range":
                    plan.Actions.Add("sort");
                    plan.Cells.Add(TargetOf(book, input.GetString("range"), plan.Sheet, null));
                    break;
                case "add_sheet":
                case "copy_sheet":
                case "delete_sheet":
                    plan.Structure = true;
                    break;
                case "change_sheet":
                    plan.Structure = input.Has("new_name") || input.Has("position") || input.Has("hidden");
                    break;
                case "insert_rows_columns":
                    if (input.Has("row") || input.Has("rows"))
                        plan.Actions.Add("insert_rows");
                    if (input.Has("column") || input.Has("columns"))
                        plan.Actions.Add("insert_columns");
                    break;
                case "delete_rows_columns":
                    if (input.Has("rows") || input.Has("columns"))
                        plan.Blocked.Add("지울 행이나 열에 잠긴 칸이 있으면 지울 수 없어요.");
                    break;
                case "format_rows_columns":
                    if (input.Has("rows"))
                        plan.Actions.Add("format_rows");
                    if (input.Has("columns"))
                        plan.Actions.Add("format_columns");
                    if (input.Has("summary_below") || input.Has("summary_right"))
                        plan.Actions.Add("format_cells");
                    break;
                case "merge_cells":
                    plan.Actions.Add("format_cells");
                    if (input.GetOptionalBool("unmerge") != true)
                        plan.Cells.Add(TargetOf(book, input.GetString("range"), plan.Sheet, null));
                    break;
                case "manage_table":
                    plan.Actions.Add("autofilter");
                    if (input.Has("range"))
                        plan.Cells.Add(TargetOf(book, input.GetString("range"), plan.Sheet, null));
                    if (input.Has("new_range"))
                        plan.Cells.Add(TargetOf(book, input.GetString("new_range"), plan.Sheet, null));
                    break;
                case "filter_range":
                    plan.Actions.Add("autofilter");
                    break;
                case "remove_duplicates":
                    plan.Blocked.Add("중복 행을 지우면 행 전체가 지워져서 잠긴 칸이 있으면 안 돼요.");
                    break;
                case "set_conditional_format":
                    plan.Actions.Add("format_cells");
                    if (input.Has("range"))
                        plan.Cells.Add(TargetOf(book, input.GetString("range"), plan.Sheet, null));
                    break;
                case "copy_range":
                    plan.Cells.Add(DestinationOf(book, input, plan.Sheet));
                    if (input.GetOptionalBool("move") == true)
                        plan.Cells.Add(TargetOf(book, input.GetString("source"), plan.Sheet, null));
                    break;
                case "shift_cells":
                    plan.Actions.AddRange(new[] { "insert_rows", "insert_columns", "delete_rows", "delete_columns" });
                    plan.Cells.Add(TargetOf(book, input.GetString("range"), plan.Sheet, null));
                    break;
                case "set_data_validation":
                    plan.Cells.Add(TargetOf(book, input.GetString("range"), plan.Sheet, null));
                    break;
                case "set_hyperlink":
                    plan.Actions.Add("insert_hyperlinks");
                    if (input.Has("cell"))
                        plan.Cells.Add(TargetOf(book, input.GetString("cell"), plan.Sheet, null));
                    break;
                case "edit_comment":
                case "create_chart":
                case "insert_drawing":
                case "change_object":
                    plan.Actions.Add("objects");
                    break;
                case "add_subtotals":
                    plan.Blocked.Add("부분합은 행을 끼워 넣고 칸을 쓰는 작업이라서 보호된 시트에서는 할 수 없어요.");
                    break;
                case "split_text":
                    plan.Blocked.Add("텍스트를 여러 칸에 나누는 작업은 칸을 많이 바꾸므로 보호된 시트에서는 할 수 없어요.");
                    break;
                case "build_pivot_summary":
                    if (input.GetOptionalString("destination") == "current_sheet")
                        plan.Blocked.Add("요약표를 현재 시트에 놓으면 보호된 칸을 덮을 수 있어서 보호된 시트에서는 할 수 없어요.");
                    else
                        plan.Structure = true;
                    break;
                case "goal_seek":
                    plan.Cells.Add(TargetOf(book, input.GetString("changing_cell"), plan.Sheet, null));
                    break;
                case "protect":
                    break;
                case "freeze_panes":
                case "set_sheet_view":
                case "set_workbook_options":
                case "set_print_setup":
                case "define_name":
                    break;
                default:
                    return null;
            }

            return plan;
        }

        private static void AddWriteTargets(ExcelBook book, PlanResult plan, ToolInput input)
        {
            var start = TargetOf(book, input.GetString("start"), plan.Sheet, null);
            var origin = start.Range;
            var values = input.Get("values");
            if (values.ValueKind == JsonValueKind.String)
            {
                try
                {
                    values = JsonDocument.Parse(values.GetString() ?? string.Empty).RootElement;
                }
                catch (JsonException)
                {
                }
            }

            if (values.ValueKind != JsonValueKind.Array)
            {
                plan.Cells.Add(start);
                return;
            }

            var row = 0;
            foreach (var line in values.EnumerateArray())
            {
                if (line.ValueKind == JsonValueKind.Array)
                {
                    var column = 0;
                    foreach (var item in line.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Null)
                            plan.Cells.Add(new CellTarget(start.Sheet, CellRange.Of(new CellAddress(origin.Left + column, origin.Top + row))));

                        column++;
                    }
                }
                else if (line.ValueKind != JsonValueKind.Null)
                {
                    plan.Cells.Add(new CellTarget(start.Sheet, CellRange.Of(new CellAddress(origin.Left, origin.Top + row))));
                }

                row++;
            }
        }

        private static CellTarget DestinationOf(ExcelBook book, ToolInput input, string sourceSheet)
        {
            var source = TargetOf(book, input.GetString("source"), sourceSheet, null).Range;
            var start = TargetOf(book, input.GetString("target"), input.GetOptionalString("target_sheet"), sourceSheet);
            var transpose = input.GetOptionalBool("transpose") == true;
            var width = transpose ? source.RowCount : source.ColumnCount;
            var height = transpose ? source.ColumnCount : source.RowCount;
            var destination = new CellRange(start.Range.Left, start.Range.Top, start.Range.Left + width - 1, start.Range.Top + height - 1);
            return new CellTarget(start.Sheet, destination);
        }

        private static CellTarget TargetOf(ExcelBook book, string text, string explicitSheet, string fallbackSheet)
        {
            var rest = text ?? string.Empty;
            string prefix = null;
            var bang = rest.LastIndexOf('!');
            if (bang > 0)
            {
                prefix = rest.Substring(0, bang).Trim();
                if (prefix.Length > 1 && prefix[0] == '\'' && prefix[^1] == '\'')
                    prefix = prefix.Substring(1, prefix.Length - 2).Replace("''", "'");

                rest = rest.Substring(bang + 1);
            }

            var sheet = string.IsNullOrWhiteSpace(explicitSheet) ? prefix ?? fallbackSheet : explicitSheet;
            if (sheet != null && book.FindSheet(sheet) == null)
                throw new ToolInputException($"‘{sheet}’ 시트를 찾을 수 없어요. 있는 시트: {book.SheetList()}");

            return new CellTarget(sheet, RangeIn(book, sheet, rest.Trim()));
        }

        private static CellRange RangeIn(ExcelBook book, string sheet, string text)
        {
            var lastRow = 0;
            var lastColumn = 0;
            foreach (var owner in sheet == null ? book.Sheets.ToList() : new List<ExcelSheet> { book.FindSheet(sheet) })
            {
                lastRow = Math.Max(lastRow, owner?.UsedRange?.Bottom ?? 0);
                lastColumn = Math.Max(lastColumn, owner?.UsedRange?.Right ?? 0);
            }

            if (!CellRange.TryParse(text, lastRow, lastColumn, out var range))
                throw new ToolInputException($"알아볼 수 없는 범위예요: ‘{text}’. B2:D10처럼 알려 주세요.");

            return range;
        }

        private sealed record SheetAction(string Name, string Label, bool LockedByDefault, Func<SheetProtection, BooleanValue> Get, Action<SheetProtection, BooleanValue> Set);

        private sealed record CellTarget(string Sheet, CellRange Range);

        private sealed class PlanResult
        {
            public string Sheet { get; set; }

            public List<string> Actions { get; } = new();

            public List<CellTarget> Cells { get; } = new();

            public List<string> Blocked { get; } = new();

            public bool Structure { get; set; }

            public bool LockEdit { get; set; }
        }
    }
}
