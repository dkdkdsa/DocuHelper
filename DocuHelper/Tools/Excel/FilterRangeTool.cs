using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class FilterRangeTool : ExcelTool
    {
        private const int MaxFilterRows = 100000;

        private const int MaxAnchors = 200;

        private const string DatabaseName = "_xlnm._FilterDatabase";

        public override string Name => "filter_range";

        public override string Description =>
            "Turn on Excel 필터 (AutoFilter) for a table, filter it, or turn it off. apply puts filter buttons on the header row of range (default: the table around the cell). "
            + "set_criteria hides the rows that do not match: give column and either values (show only these; \"(빈 셀)\" = empty cells) or condition; it also turns the filter on when range is given. "
            + "Criteria on several columns are AND-combined. clear_criteria shows all rows again (one column or all), reapply recalculates which rows match after the data changed, remove deletes the filter and shows every row. "
            + "One filter per sheet (or per table). Hidden rows keep their data; formulas still see them (SUBTOTAL skips them).";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "action": {"type": "string", "description": "apply (default), set_criteria, clear_criteria, reapply or remove.", "enum": ["apply", "set_criteria", "clear_criteria", "reapply", "remove"]},
                "range": {"type": "string", "description": "apply: the table including the header row, e.g. \"A1:F41\". Other actions: any cell of the filtered table when the sheet has several filters."},
                "column": {"type": "string", "description": "set_criteria / clear_criteria: column letter (e.g. \"B\") or the header text."},
                "values": {"type": "array", "description": "Show only rows whose cell text is one of these. Use \"(빈 셀)\" for empty cells.", "items": {"type": "string", "description": "Cell text as displayed."}},
                "condition": {
                  "type": "object",
                  "description": "Custom condition instead of values.",
                  "properties": {
                    "operator": {"type": "string", "description": "Comparison.", "enum": ["equal", "not_equal", "greater_than", "greater_equal", "less_than", "less_equal", "begins_with", "ends_with", "contains", "not_contains", "between", "top", "bottom", "above_average", "below_average", "blank", "non_blank"]},
                    "value": {"type": "string", "description": "Value to compare with."},
                    "value2": {"type": "string", "description": "Upper value for between."},
                    "count": {"type": "integer", "description": "top/bottom: number of items (or percent).", "minimum": 1, "maximum": 1000},
                    "percent": {"type": "boolean", "description": "top/bottom: count is a percent."},
                    "combine": {"type": "string", "description": "With a second condition: and / or.", "enum": ["and", "or"]},
                    "operator2": {"type": "string", "description": "Second comparison (same list, not between/top/bottom/average/blank)."},
                    "value3": {"type": "string", "description": "Second value."}
                  }
                }
              },
              "required": ["sheet"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", true);
            var action = (input.GetOptionalString("action") ?? "apply").Trim().ToLowerInvariant();
            return action switch
            {
                "apply" => Apply(book, sheet, input),
                "set_criteria" => SetCriteria(book, sheet, input),
                "clear_criteria" => ClearCriteria(book, sheet, input),
                "reapply" => Reapply(book, sheet, input),
                "remove" => Remove(book, sheet, input),
                _ => throw new ToolInputException($"알 수 없는 action이에요: {action}. apply, set_criteria, clear_criteria, reapply, remove 중에서 골라 주세요."),
            };
        }

        private static ToolResult Apply(ExcelBook book, ExcelSheet sheet, ToolInput input)
        {
            var target = Locate(book, sheet, input, true);
            if (!target.IsNew)
                return ToolResult.Ok($"‘{sheet.Name}’ already has a filter on {target.Reference}; nothing changed.", "그대로");

            Attach(book, sheet, target);
            var content = $"Turned on the filter for ‘{sheet.Name}’ {target.Reference}: filter buttons on header row {target.Reference.Top + 1}, {DataRows(target)} data row(s), no criteria yet."
                + (target.Table != null ? $" The filter belongs to table ‘{target.Table.Name}’." : string.Empty);
            return ToolResult.Changed(content, Change(sheet, target), "필터 켬");
        }

        private static ToolResult SetCriteria(ExcelBook book, ExcelSheet sheet, ToolInput input)
        {
            var target = Locate(book, sheet, input, false);
            if (target == null)
            {
                if (string.IsNullOrWhiteSpace(input.GetOptionalString("range")))
                    throw new ToolInputException("이 시트에는 필터가 없어요. action apply로 먼저 필터를 켜거나, range에 표 범위를 함께 알려 주세요.");

                target = Locate(book, sheet, input, true);
            }

            var column = RangeColumns.Resolve(book, sheet, target.Reference, true, input.GetString("column"), "column", "필터 열은");
            var id = (uint)(column - target.Reference.Left);
            var built = FilterRules.Build(input, id);
            var existing = target.Filter.Elements<FilterColumn>().FirstOrDefault(c => (c.ColumnId?.Value ?? 0u) == id);
            var others = target.Filter.Elements<FilterColumn>().Where(c => c != existing).ToList();
            var prospective = built == null ? others : others.Append(built).ToList();
            var plan = Evaluate(book, sheet, target.Reference, prospective);

            Attach(book, sheet, target);
            existing?.Remove();
            if (built != null)
                Insert(target.Filter, built);

            var changed = Commit(sheet, target.Reference, plan);
            var updated = changed ? Recalculate(book, new List<CellKey>()).Updated : 0;
            var label = CellAddress.ColumnName(column);
            var summary = built == null
                ? $"Cleared the criteria of column {label}."
                : $"Filtered column {label}: {FilterRules.Describe(built)}.";
            var content = $"{summary} {Counts(plan)}{HiddenText(plan)}{Recalculated(updated)}";
            return ToolResult.Changed(content, Change(sheet, target), CountDetail(plan, prospective.Count > 0));
        }

        private static ToolResult ClearCriteria(ExcelBook book, ExcelSheet sheet, ToolInput input)
        {
            var target = Require(book, sheet, input);
            var columns = target.Filter.Elements<FilterColumn>().ToList();
            string where;
            if (!string.IsNullOrWhiteSpace(input.GetOptionalString("column")))
            {
                var column = RangeColumns.Resolve(book, sheet, target.Reference, true, input.GetString("column"), "column", "필터 열은");
                var id = (uint)(column - target.Reference.Left);
                columns = columns.Where(c => (c.ColumnId?.Value ?? 0u) == id).ToList();
                where = "column " + CellAddress.ColumnName(column);
            }
            else
            {
                where = "all columns";
            }

            if (columns.Count == 0)
                return ToolResult.Ok($"The filter on {target.Reference} has no criteria on {where}; nothing changed.", "그대로");

            var remaining = target.Filter.Elements<FilterColumn>().Where(c => !columns.Contains(c)).ToList();
            var plan = Evaluate(book, sheet, target.Reference, remaining);
            foreach (var column in columns)
                column.Remove();

            var changed = Commit(sheet, target.Reference, plan);
            var updated = changed ? Recalculate(book, new List<CellKey>()).Updated : 0;
            var content = $"Cleared the filter criteria of {where} on ‘{sheet.Name}’ {target.Reference}. {Counts(plan)}{HiddenText(plan)}{Recalculated(updated)}";
            return ToolResult.Changed(content, Change(sheet, target), CountDetail(plan, remaining.Count > 0));
        }

        private static ToolResult Reapply(ExcelBook book, ExcelSheet sheet, ToolInput input)
        {
            var target = Require(book, sheet, input);
            var columns = target.Filter.Elements<FilterColumn>().ToList();
            if (columns.Count == 0)
                return ToolResult.Ok($"The filter on {target.Reference} has no criteria, so there is nothing to reapply.", "그대로");

            var plan = Evaluate(book, sheet, target.Reference, columns);
            var changed = Commit(sheet, target.Reference, plan);
            var updated = changed ? Recalculate(book, new List<CellKey>()).Updated : 0;
            var content = $"Reapplied the filter on ‘{sheet.Name}’ {target.Reference}. {Counts(plan)}{HiddenText(plan)}{Recalculated(updated)}";
            return ToolResult.Changed(content, Change(sheet, target), CountDetail(plan, true));
        }

        private static ToolResult Remove(ExcelBook book, ExcelSheet sheet, ToolInput input)
        {
            var target = Locate(book, sheet, input, false);
            if (target == null)
                return ToolResult.Ok($"‘{sheet.Name}’ has no filter; nothing changed.", "그대로");

            var reference = target.Reference;
            var shown = 0;
            if (target.Table == null)
            {
                shown = ExcelTables.ReleaseFilter(book, sheet, reference);
            }
            else
            {
                var criteria = target.Filter.Elements<FilterColumn>().Any();
                target.Table.Definition.AutoFilter = null;
                target.Table.Definition.SortState = null;
                if (criteria)
                    shown = ExcelTables.Reveal(sheet, reference);
            }

            var updated = shown > 0 ? Recalculate(book, new List<CellKey>()).Updated : 0;
            sheet.Invalidate();
            var content = $"Removed the filter from ‘{sheet.Name}’ {reference}."
                + (shown > 0 ? $" {shown} hidden row(s) are shown again." : string.Empty)
                + Recalculated(updated);
            return ToolResult.Changed(content, Change(sheet, target), "필터 끔");
        }

        private static Target Require(ExcelBook book, ExcelSheet sheet, ToolInput input) =>
            Locate(book, sheet, input, false) ?? throw new ToolInputException("이 시트에는 필터가 없어요. action apply로 먼저 필터를 켜 주세요.");

        private static List<Target> Targets(ExcelSheet sheet)
        {
            var targets = new List<Target>();
            var sheetFilter = sheet.Worksheet.GetFirstChild<AutoFilter>();
            if (sheetFilter != null && CellRange.TryParse(sheetFilter.Reference?.Value, out var range))
                targets.Add(new Target(sheetFilter, range, null, false));

            foreach (var table in ExcelTables.On(sheet))
            {
                var filter = table.Definition.AutoFilter;
                if (filter == null)
                    continue;

                var reference = CellRange.TryParse(filter.Reference?.Value, out var parsed) ? parsed : TableFilterRange(table);
                targets.Add(new Target(filter, reference, table, false));
            }

            return targets;
        }

        private static Target Locate(ExcelBook book, ExcelSheet sheet, ToolInput input, bool create)
        {
            var text = input.GetOptionalString("range");
            var given = string.IsNullOrWhiteSpace(text) ? null : ParseRange(sheet, text);
            var targets = Targets(sheet);
            var matches = given == null
                ? targets
                : targets.Where(t => t.Reference.Intersects(given) || (t.Table != null && t.Table.Range.Intersects(given))).ToList();

            if (!create)
                return Pick(sheet, matches);

            if (given == null && targets.Count > 0)
                return Pick(sheet, targets);

            if (given != null && !given.IsSingleCell)
            {
                var same = matches.FirstOrDefault(t => t.Reference.ToString() == given.ToString() || (t.Table != null && t.Table.Range.Contains(given)));
                if (same != null)
                    return same;
            }
            else if (matches.Count > 0)
            {
                return Pick(sheet, matches);
            }

            return Create(book, sheet, given, targets);
        }

        private static Target Pick(ExcelSheet sheet, List<Target> candidates)
        {
            if (candidates.Count == 0)
                return null;

            if (candidates.Count == 1)
                return candidates[0];

            throw new ToolInputException($"‘{sheet.Name}’ 시트에 필터가 여러 개예요({string.Join(", ", candidates.Select(c => c.Reference))}). range에 그중 한 곳의 칸을 알려 주세요.");
        }

        private static Target Create(ExcelBook book, ExcelSheet sheet, CellRange given, List<Target> targets)
        {
            var tables = ExcelTables.On(sheet);
            CellRange range;
            if (given != null && !given.IsSingleCell)
            {
                range = given;
            }
            else
            {
                var start = given != null ? given.Start : (CellAddress?)null;
                var table = start.HasValue ? tables.FirstOrDefault(t => t.Contains(start.Value.Column, start.Value.Row)) : null;
                if (table != null)
                    return ForTable(table);

                range = FindRegion(sheet, start);
            }

            var overlapping = tables.Where(t => t.Range.Intersects(range)).ToList();
            if (overlapping.Count > 0)
            {
                var table = overlapping[0];
                if (overlapping.Count > 1 || !table.Range.Contains(range))
                    throw new ToolInputException($"범위 {range}가 ‘{table.Name}’ 표({table.Range})에 일부만 걸쳐 있어요. 표 안의 한 칸을 알려 주거나, 표 전체({table.Range})를 range로 알려 주세요.");

                return ForTable(table);
            }

            var existing = targets.FirstOrDefault(t => t.Table == null);
            if (existing != null)
                throw new ToolInputException($"이 시트에는 이미 필터({existing.Reference})가 있어요. 한 시트에는 필터를 하나만 둘 수 있으니, remove로 끈 뒤 다시 켜 주세요.");

            var data = new CellRange(range.Left, range.Top + 1, range.Right, range.Bottom);
            if (!Enumerable.Range(range.Left, range.ColumnCount).Any(c => ExcelSheet.HasContent(sheet.FindCell(c, range.Top))))
                throw new ToolInputException($"첫 줄({range.Top + 1}행)이 비어 있어요. 필터는 머리글이 있는 줄에 달려서, 머리글 줄부터 범위를 알려 주세요.");

            if (range.RowCount > 1)
            {
                var merged = sheet.MergedRanges().FirstOrDefault(m => m.RowCount > 1 && m.Intersects(data));
                if (merged != null)
                    throw new ToolInputException($"여러 줄에 걸쳐 합쳐진 칸({merged})이 있어서 필터를 걸 수 없어요. merge_cells의 unmerge로 먼저 풀어 주세요.");
            }

            return new Target(new AutoFilter { Reference = range.ToString() }, range, null, true);
        }

        private static Target ForTable(ExcelTable table)
        {
            var existing = table.Definition.AutoFilter;
            if (existing != null && CellRange.TryParse(existing.Reference?.Value, out var reference))
                return new Target(existing, reference, table, false);

            if (!table.HasHeader)
                throw new ToolInputException($"‘{table.Name}’ 표에는 머리글 줄이 없어서 필터 버튼을 달 수 없어요.");

            var range = TableFilterRange(table);
            return new Target(existing ?? new AutoFilter { Reference = range.ToString() }, range, table, existing == null);
        }

        private static CellRange TableFilterRange(ExcelTable table) =>
            new(table.Range.Left, table.Range.Top, table.Range.Right, table.HasTotals ? table.Range.Bottom - 1 : table.Range.Bottom);

        private static CellRange FindRegion(ExcelSheet sheet, CellAddress? start)
        {
            var filled = new HashSet<(int Column, int Row)>();
            foreach (var entry in sheet.Entries)
            {
                if (ExcelSheet.HasContent(entry.Cell))
                    filled.Add((entry.Column, entry.Row));
            }

            if (filled.Count == 0)
                throw new ToolInputException("비어 있는 시트라서 필터를 걸 수 없어요. 먼저 표를 써 넣어 주세요.");

            if (start.HasValue)
            {
                var region = Region(filled, start.Value);
                if (region.IsSingleCell)
                    throw new ToolInputException($"{start.Value} 칸 둘레에서 표를 찾지 못했어요. 머리글 줄을 포함한 범위를 range로 알려 주세요. 예: A1:F41");

                return region;
            }

            var skipped = new List<CellRange>();
            foreach (var cell in filled.OrderBy(c => c.Row).ThenBy(c => c.Column).Take(5000))
            {
                if (skipped.Any(r => r.Contains(cell.Column, cell.Row)))
                    continue;

                var region = Region(filled, new CellAddress(cell.Column, cell.Row));
                if (region.RowCount >= 2 && region.ColumnCount >= 2)
                    return region;

                skipped.Add(region);
                if (skipped.Count >= 50)
                    break;
            }

            throw new ToolInputException("시트에서 표를 찾지 못했어요. 머리글 줄을 포함한 범위를 range로 알려 주세요. 예: A1:F41");
        }

        private static CellRange Region(HashSet<(int Column, int Row)> filled, CellAddress start)
        {
            int left = start.Column, top = start.Row, right = start.Column, bottom = start.Row;
            bool grew;
            do
            {
                grew = false;
                if (top > 0 && Enumerable.Range(left - 1, right - left + 3).Any(c => filled.Contains((c, top - 1))))
                {
                    top--;
                    grew = true;
                }

                if (bottom < CellAddress.MaxRows - 1 && Enumerable.Range(left - 1, right - left + 3).Any(c => filled.Contains((c, bottom + 1))))
                {
                    bottom++;
                    grew = true;
                }

                if (left > 0 && Enumerable.Range(top - 1, bottom - top + 3).Any(r => filled.Contains((left - 1, r))))
                {
                    left--;
                    grew = true;
                }

                if (right < CellAddress.MaxColumns - 1 && Enumerable.Range(top - 1, bottom - top + 3).Any(r => filled.Contains((right + 1, r))))
                {
                    right++;
                    grew = true;
                }
            }
            while (grew);

            return new CellRange(left, top, right, bottom);
        }

        private static void Attach(ExcelBook book, ExcelSheet sheet, Target target)
        {
            if (!target.IsNew)
                return;

            target.IsNew = false;
            if (target.Table != null)
            {
                target.Table.Definition.AutoFilter = target.Filter;
                return;
            }

            WorksheetOrder.Insert(sheet.Worksheet, target.Filter);
            var workbook = book.WorkbookPart.Workbook;
            var names = workbook.DefinedNames;
            if (names == null)
            {
                names = new DefinedNames();
                workbook.DefinedNames = names;
            }

            var text = FormulaReference.QuoteSheet(sheet.Name) + "!" + Absolute(target.Reference);
            var name = names.Elements<DefinedName>().FirstOrDefault(n => n.Name?.Value == DatabaseName && n.LocalSheetId?.Value == (uint)sheet.Position);
            if (name == null)
                names.Append(new DefinedName { Name = DatabaseName, LocalSheetId = (uint)sheet.Position, Hidden = true, Text = text });
            else
                name.Text = text;
        }

        private static string Absolute(CellRange range) =>
            $"${CellAddress.ColumnName(range.Left)}${range.Top + 1}:${CellAddress.ColumnName(range.Right)}${range.Bottom + 1}";

        private static void Insert(AutoFilter filter, FilterColumn column)
        {
            var id = column.ColumnId?.Value ?? 0u;
            var columns = filter.Elements<FilterColumn>().ToList();
            var later = columns.FirstOrDefault(c => (c.ColumnId?.Value ?? 0u) > id);
            if (later != null)
                later.InsertBeforeSelf(column);
            else if (columns.Count > 0)
                columns[^1].InsertAfterSelf(column);
            else
                filter.PrependChild(column);
        }

        private static Evaluation Evaluate(ExcelBook book, ExcelSheet sheet, CellRange reference, IReadOnlyList<FilterColumn> columns)
        {
            var first = reference.Top + 1;
            var total = Math.Max(0, reference.Bottom - first + 1);
            var plan = new Evaluation { Total = total, Active = columns.Count > 0 };
            if (columns.Count == 0)
                return plan;

            if (total > MaxFilterRows)
                throw new ToolInputException($"필터 범위가 너무 커요. 데이터 {MaxFilterRows}줄까지만 조건을 걸 수 있어요.");

            var tests = new List<(int Column, Func<string, ExcelValue, bool> Test, (string Text, ExcelValue Value)[] Cells)>();
            foreach (var column in columns)
            {
                var index = (int)(column.ColumnId?.Value ?? 0u);
                var sheetColumn = reference.Left + index;
                if (sheetColumn > reference.Right)
                    continue;

                var cells = new (string Text, ExcelValue Value)[total];
                for (var i = 0; i < total; i++)
                {
                    var cell = sheet.FindCell(sheetColumn, first + i);
                    if (cell == null)
                    {
                        cells[i] = (string.Empty, ExcelValue.Blank);
                        continue;
                    }

                    var value = book.ReadValue(cell);
                    cells[i] = (book.DisplayText(value, book.StyleOf(cell)), value);
                }

                var test = FilterRules.Compile(column, cells, CellAddress.ColumnName(sheetColumn) + "열");
                if (test != null)
                    tests.Add((sheetColumn, test, cells));
            }

            for (var i = 0; i < total; i++)
            {
                var visible = true;
                foreach (var (_, test, cells) in tests)
                {
                    if (!test(cells[i].Text, cells[i].Value))
                    {
                        visible = false;
                        break;
                    }
                }

                if (!visible)
                    plan.Hidden.Add(first + i);
            }

            plan.Active = tests.Count > 0;
            return plan;
        }

        private static bool Commit(ExcelSheet sheet, CellRange reference, Evaluation plan)
        {
            var changed = false;
            var hidden = new HashSet<int>(plan.Hidden);
            for (var row = reference.Top + 1; row <= reference.Bottom; row++)
            {
                var want = hidden.Contains(row);
                if (sheet.IsRowHidden(row) == want)
                    continue;

                sheet.SetRowHidden(row, want);
                changed = true;
            }

            if (changed)
                sheet.Invalidate();

            return changed;
        }

        private static int DataRows(Target target) => Math.Max(0, target.Reference.Bottom - target.Reference.Top);

        private static DocuChange Change(ExcelSheet sheet, Target target)
        {
            var header = Enumerable.Range(target.Reference.Left, target.Reference.ColumnCount)
                .Take(MaxAnchors)
                .Select(c => new CellAddress(c, target.Reference.Top));
            return new DocuChange
            {
                Location = target.Reference + " 필터",
                Icon = "filter",
                Anchors = Anchors(sheet, header),
            };
        }

        private static string Counts(Evaluation plan)
        {
            if (!plan.Active)
                return $"All {plan.Total} data row(s) are shown.";

            var shown = plan.Total - plan.Hidden.Count;
            return shown == 0
                ? $"No row matches: 0 of {plan.Total} data row(s) are shown."
                : $"{shown} of {plan.Total} data row(s) are shown.";
        }

        private static string HiddenText(Evaluation plan)
        {
            if (plan.Hidden.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            var start = plan.Hidden[0];
            var previous = start;
            foreach (var row in plan.Hidden.Skip(1).Append(int.MaxValue))
            {
                if (row == previous + 1)
                {
                    previous = row;
                    continue;
                }

                parts.Add(start == previous ? (start + 1).ToString() : (start + 1) + "-" + (previous + 1));
                if (parts.Count >= 20 || row == int.MaxValue)
                    break;

                start = row;
                previous = row;
            }

            return " Hidden rows: " + string.Join(", ", parts) + (parts.Count >= 20 ? ", …" : string.Empty) + ".";
        }

        private static string Recalculated(int updated) =>
            updated > 0 ? $" {updated} formula cell(s) were recalculated for the new visible rows." : string.Empty;

        private static string CountDetail(Evaluation plan, bool active) =>
            active ? $"{plan.Total}개 중 {plan.Total - plan.Hidden.Count}개를 찾았어요" : "모두 보여요";

        private sealed class Target
        {
            public Target(AutoFilter filter, CellRange reference, ExcelTable table, bool isNew)
            {
                Filter = filter;
                Reference = reference;
                Table = table;
                IsNew = isNew;
            }

            public AutoFilter Filter { get; }

            public CellRange Reference { get; }

            public ExcelTable Table { get; }

            public bool IsNew { get; set; }
        }

        private sealed class Evaluation
        {
            public int Total { get; init; }

            public bool Active { get; set; }

            public List<int> Hidden { get; } = new();
        }
    }
}
