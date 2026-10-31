using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class ManageTableTool : ExcelTool
    {
        private const int MaxAnchors = 200;

        public override string Name => "manage_table";

        public override string Description =>
            "Turn a range into an Excel table (표, list object) or change one: banded rows, header styling and filter buttons come with it. "
            + "action create (default) needs range including the header row (header cells become text; empty or duplicate headers are renamed like Excel: 열3, 이름2) and optionally name and style (default TableStyleMedium2). "
            + "set_options changes style, banded_rows, banded_columns, first_column, last_column, total_row and filter_buttons. resize moves the table to new_range (same header row; refused while a total row is shown). "
            + "convert_to_range dissolves the table, keeps its look as cell formatting and turns structured references into cell addresses. "
            + "Refuses overlaps with other tables, merged cells, array formulas and pivot tables. Formulas that use structured references like 표1[매출] are not calculated here, so write plain ranges in formulas.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "action": {"type": "string", "enum": ["create", "convert_to_range", "set_options", "resize"], "description": "create (default), convert_to_range, set_options or resize."},
                "range": {"type": "string", "description": "create: the data including the header row, e.g. \"A1:F41\". Other actions: any cell inside the table."},
                "table": {"type": "string", "description": "Existing table name for set_options/resize/convert_to_range (default: the table that contains range)."},
                "name": {"type": "string", "description": "Name for a new table. Letters, digits, underscore, Korean allowed; no spaces; unique in the workbook. Default 표1, 표2, ..."},
                "header": {"type": "boolean", "description": "The first row is a header row. Must be true: the tool does not insert a header row (insert one first with insert_rows_columns)."},
                "style": {"type": "string", "description": "TableStyleLight1, Light9-Light14, Medium2-Medium7, Medium9, Dark1, Dark2, or \"none\". Default TableStyleMedium2."},
                "banded_rows": {"type": "boolean", "description": "Alternating row shading. Default true."},
                "banded_columns": {"type": "boolean", "description": "Alternating column shading. Default false."},
                "first_column": {"type": "boolean", "description": "Emphasise the first column."},
                "last_column": {"type": "boolean", "description": "Emphasise the last column."},
                "total_row": {"type": "boolean", "description": "Show a total row under the data (the row below must be empty). It shows 요약 and SUBTOTAL(109) of the last numeric column."},
                "filter_buttons": {"type": "boolean", "description": "Show the filter buttons in the header row. Default true."},
                "new_range": {"type": "string", "description": "resize: the new range including the header row."}
              },
              "required": ["sheet"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var action = (input.GetOptionalString("action") ?? "create").Trim().ToLowerInvariant();
            return action switch
            {
                "create" => Create(book, input),
                "convert_to_range" => ConvertToRange(book, input),
                "set_options" => SetOptions(book, input),
                "resize" => Resize(book, input),
                _ => throw new ToolInputException("action은 create, convert_to_range, set_options, resize 중 하나로 알려 주세요."),
            };
        }

        private static ToolResult Create(ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", true);
            var range = ParseRange(sheet, input.GetString("range"));
            if (input.GetOptionalBool("header") == false)
                throw new ToolInputException("표에는 머리글 줄이 꼭 있어야 해요. 범위 맨 윗줄에 열 이름을 쓰고 header를 true로 알려 주세요. 머리글 줄이 없다면 insert_rows_columns로 먼저 줄을 넣어 주세요.");

            if (range.RowCount < 2)
                throw new ToolInputException($"머리글 줄 아래에 데이터 줄이 한 줄 이상 있어야 해요. {range}에 데이터 줄까지 포함해서 다시 알려 주세요. 예: A1:F41");

            var style = ReadStyle(input) ?? TableStyleCatalog.DefaultName;
            var name = input.Has("name") ? ExcelTables.CheckName(book, input.GetString("name"), null) : ExcelTables.NextName(book);
            var options = TableOptions.Read(input);
            ExcelTables.CheckFree(sheet, range, null);
            if (options.TotalRow == true)
                ExcelTables.CheckTotalsRoom(sheet, range, null);

            var plan = TableHeaders.Plan(book, sheet, range.Left, range.Right, range.Top);
            sheet.PrepareForWrite();
            var written = new List<CellKey>();
            var shown = ExcelTables.ReleaseFilter(book, sheet, range);
            TableHeaders.Write(book, sheet, range.Left, range.Top, plan, written);
            var table = ExcelTables.Add(book, sheet, range, plan.Names, name, style, options.FilterButtons != false);
            ExcelTables.SetStyleFlags(table.Definition, null, options.BandedRows, options.BandedColumns, options.FirstColumn, options.LastColumn);
            if (options.TotalRow == true)
                TableTotals.Show(book, table);

            sheet.UpdateDimension();
            Finish(book, written);
            table = ExcelTables.Find(book, name);

            var content = $"Created table ‘{name}’ on ‘{sheet.Name}’ over {table.Range} ({table.Range.RowCount - 1 - (table.HasTotals ? 1 : 0)} data row(s), {plan.Names.Count} column(s)) with style {(string.IsNullOrEmpty(style) ? "none" : style)}. "
                + $"Columns: {string.Join(", ", plan.Names)}."
                + (plan.Notes.Count > 0 ? $" Header cells rewritten as text: {string.Join("; ", plan.Notes)}." : string.Empty)
                + (table.HasTotals ? $" Total row {table.TotalsRange.Start.Row + 1} added." : string.Empty)
                + (shown > 0 ? $" The sheet filter was removed and {shown} hidden row(s) were shown again." : string.Empty)
                + Reminder;
            return ToolResult.Changed(content, Change(sheet, table), $"표 만들기: {name}");
        }

        private static ToolResult SetOptions(ExcelBook book, ToolInput input)
        {
            var table = FindTable(book, input);
            var sheet = table.Sheet;
            var style = ReadStyle(input);
            var options = TableOptions.Read(input);
            if (style == null && !options.Any)
                throw new ToolInputException("바꿀 항목을 알려 주세요. style, banded_rows, banded_columns, first_column, last_column, total_row, filter_buttons 중에서 골라요.");

            if (table.IsLinked && (options.TotalRow.HasValue || options.FilterButtons.HasValue))
                throw new ToolInputException($"‘{table.Name}’ 표는 외부 데이터에 연결되어 있어서 요약 줄과 필터 단추는 여기서 바꿀 수 없어요. 모양 옵션만 바꿀 수 있어요.");

            if (options.FilterButtons == true && !table.HasHeader)
                throw new ToolInputException($"‘{table.Name}’ 표에는 머리글 줄이 없어서 필터 단추를 켤 수 없어요.");

            var turnOn = options.TotalRow == true && !table.HasTotals;
            var turnOff = options.TotalRow == false && table.HasTotals;
            if (turnOn)
                ExcelTables.CheckTotalsRoom(sheet, table.Range, table);

            var notes = new List<string>();
            sheet.PrepareForWrite();
            ExcelTables.SetStyleFlags(table.Definition, style, options.BandedRows, options.BandedColumns, options.FirstColumn, options.LastColumn);
            if (style != null)
                notes.Add(style.Length == 0 ? "style none" : "style " + style);

            if (options.BandedRows.HasValue)
                notes.Add("banded_rows " + Flag(options.BandedRows.Value));

            if (options.BandedColumns.HasValue)
                notes.Add("banded_columns " + Flag(options.BandedColumns.Value));

            if (options.FirstColumn.HasValue)
                notes.Add("first_column " + Flag(options.FirstColumn.Value));

            if (options.LastColumn.HasValue)
                notes.Add("last_column " + Flag(options.LastColumn.Value));

            if (options.FilterButtons.HasValue)
            {
                var data = new CellRange(table.Range.Left, table.Range.Top, table.Range.Right, table.HasTotals ? table.Range.Bottom - 1 : table.Range.Bottom);
                if (options.FilterButtons == true)
                {
                    table.Definition.AutoFilter ??= new AutoFilter { Reference = data.ToString() };
                }
                else if (table.Definition.AutoFilter != null)
                {
                    var criteria = table.Definition.AutoFilter.Elements<FilterColumn>().Any();
                    table.Definition.AutoFilter = null;
                    table.Definition.SortState = null;
                    if (criteria)
                        ExcelTables.Reveal(sheet, data);
                }

                notes.Add("filter_buttons " + Flag(options.FilterButtons.Value));
            }

            if (turnOn)
            {
                TableTotals.Show(book, table);
                notes.Add("total_row on");
            }
            else if (turnOff)
            {
                TableTotals.Hide(book, table);
                notes.Add("total_row off (the total row cells were cleared)");
            }

            if (options.TotalRow.HasValue && !turnOn && !turnOff)
                notes.Add("total_row already " + Flag(options.TotalRow.Value));

            Finish(book, new List<CellKey>());
            var current = ExcelTables.Find(book, table.Name);
            var content = $"Updated table ‘{current.Name}’ on ‘{sheet.Name}’ ({current.Range}): {string.Join(", ", notes)}. Style now {(string.IsNullOrEmpty(current.StyleName) ? "none" : current.StyleName)}.";
            return ToolResult.Changed(content, Change(sheet, current), "표 옵션 바꿈");
        }

        private static ToolResult Resize(ExcelBook book, ToolInput input)
        {
            var table = FindTable(book, input);
            var sheet = table.Sheet;
            var old = table.Range;
            if (table.IsLinked)
                throw new ToolInputException($"‘{table.Name}’ 표는 외부 데이터에 연결되어 있어서 크기를 바꿀 수 없어요.");

            if (table.HasTotals)
                throw new ToolInputException($"‘{table.Name}’ 표에 요약 줄이 켜져 있어서 크기를 바꿀 수 없어요. total_row를 false로 끄고 크기를 바꾼 뒤 다시 켜 주세요.");

            var next = ParseRange(sheet, input.GetString("new_range"));
            if (next.Top != old.Top)
                throw new ToolInputException($"머리글 줄은 그대로여야 해서 새 범위는 {old.Top + 1}행에서 시작해야 해요. 예: {new CellRange(next.Left, old.Top, next.Right, next.Bottom)}");

            if (next.Left > old.Right || next.Right < old.Left)
                throw new ToolInputException($"새 범위가 기존 표({old})와 겹쳐야 해요. 겹치는 열을 포함해 다시 알려 주세요.");

            if (next.RowCount < 2)
                throw new ToolInputException("머리글 줄 아래에 데이터 줄이 한 줄 이상 있어야 해요.");

            if (next.ToString() == old.ToString())
                return ToolResult.Ok($"Table ‘{table.Name}’ already covers {old}, so nothing changed.", "그대로");

            ExcelTables.CheckFree(sheet, next, table);
            var plan = TableHeaders.Plan(book, sheet, next.Left, next.Right, next.Top);
            sheet.PrepareForWrite();
            var written = new List<CellKey>();
            var shown = ExcelTables.ReleaseFilter(book, sheet, next);
            TableHeaders.Write(book, sheet, next.Left, next.Top, plan, written);

            var definition = table.Definition;
            var columns = definition.TableColumns;
            var existing = columns.Elements<TableColumn>().ToList();
            var nextId = existing.Select(c => c.Id?.Value ?? 0u).DefaultIfEmpty(0u).Max();
            columns.RemoveAllChildren();
            for (var column = next.Left; column <= next.Right; column++)
            {
                var index = column - old.Left;
                var item = index >= 0 && index < existing.Count ? existing[index] : new TableColumn { Id = ++nextId };
                item.Name = plan.Names[column - next.Left];
                columns.Append(item);
            }

            columns.Count = (uint)next.ColumnCount;
            definition.Reference = next.ToString();
            var filter = definition.AutoFilter;
            if (filter != null)
            {
                if (filter.Elements<FilterColumn>().Any())
                {
                    filter.RemoveAllChildren<FilterColumn>();
                    shown += ExcelTables.Reveal(sheet, old.Union(next));
                }

                filter.Reference = next.ToString();
            }

            if (definition.SortState != null && !(CellRange.TryParse(definition.SortState.Reference?.Value, out var sorted) && next.Contains(sorted)))
                definition.SortState = null;

            sheet.UpdateDimension();
            Finish(book, written);
            var current = ExcelTables.Find(book, table.Name);
            var content = $"Resized table ‘{current.Name}’ on ‘{sheet.Name}’ from {old} to {current.Range}."
                + (plan.Notes.Count > 0 ? $" Header cells rewritten as text: {string.Join("; ", plan.Notes)}." : string.Empty)
                + (shown > 0 ? $" The sheet filter was removed or cleared and {shown} hidden row(s) were shown again." : string.Empty)
                + Reminder;
            return ToolResult.Changed(content, Change(sheet, current), "표 크기 바꿈");
        }

        private static ToolResult ConvertToRange(ExcelBook book, ToolInput input)
        {
            var table = FindTable(book, input);
            var sheet = table.Sheet;
            if (table.IsLinked)
                throw new ToolInputException($"‘{table.Name}’ 표는 외부 데이터에 연결되어 있어서 범위로 바꿀 수 없어요. 엑셀에서 해 주세요.");

            var rewrites = TableConversion.Plan(book, table);
            var name = table.Name;
            var range = table.Range;
            var styleName = table.StyleName;
            var known = TableStyleCatalog.Contains(styleName);
            sheet.PrepareForWrite();
            var baked = TableConversion.Bake(book, table);
            TableConversion.Apply(rewrites);
            var shown = table.Definition.AutoFilter?.Elements<FilterColumn>().Any() == true ? ExcelTables.Reveal(sheet, range) : 0;
            ExcelTables.Remove(sheet, table);
            sheet.UpdateDimension();
            var written = rewrites.Where(r => r.Sheet != null).Select(r => new CellKey(r.Sheet.Index, r.Column, r.Row)).ToList();
            Finish(book, written, false);

            var content = $"Converted table ‘{name}’ on ‘{sheet.Name}’ ({range}) back to a normal range."
                + (baked > 0 ? $" {baked} cell(s) keep the table style as direct formatting." : string.Empty)
                + (!known && !string.IsNullOrEmpty(styleName) ? $" The style {styleName} is not one this tool can copy, so the cells keep only their own formatting." : string.Empty)
                + (rewrites.Count > 0 ? $" {rewrites.Count} formula(s) that used structured references now use cell addresses." : string.Empty)
                + (shown > 0 ? $" {shown} hidden row(s) were shown again." : string.Empty);
            var change = new DocuChange
            {
                Location = range.ToString(),
                Icon = "table",
                Anchors = Anchors(sheet, new CellRange(range.Left, range.Top, range.Right, range.Top).Cells().Take(MaxAnchors)),
            };
            return ToolResult.Changed(content, change, "범위로 바꿈");
        }

        private static string Reminder =>
            " Structured references like 표1[열] are not calculated here: use plain cell ranges in formulas.";

        private static string Flag(bool value) => value ? "on" : "off";

        private static string ReadStyle(ToolInput input)
        {
            var text = input.GetOptionalString("style");
            if (text == null)
                return null;

            if (!TableStyleCatalog.TryNormalize(text, out var name))
                throw new ToolInputException($"‘{text.Trim()}’ 표 스타일은 쓸 수 없어요. 쓸 수 있는 스타일: {TableStyleCatalog.NameList}, none");

            return name;
        }

        private static ExcelTable FindTable(ExcelBook book, ToolInput input)
        {
            var tables = ExcelTables.All(book);
            var name = input.GetOptionalString("table");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return ExcelTables.Find(book, name)
                    ?? throw new ToolInputException($"‘{name.Trim()}’ 표를 찾을 수 없어요. {Listing(tables)}");
            }

            var sheet = ResolveSheet(book, input, "range", true);
            var text = input.GetOptionalString("range");
            if (string.IsNullOrWhiteSpace(text))
            {
                var here = ExcelTables.On(sheet);
                if (here.Count == 1)
                    return here[0];

                throw new ToolInputException($"어느 표인지 table에 표 이름을 알려 주세요. {Listing(tables)}");
            }

            var address = ParseCell(text);
            return ExcelTables.At(sheet, address.Column, address.Row)
                ?? throw new ToolInputException($"‘{sheet.Name}’ 시트의 {address} 칸은 표 안에 있지 않아요. {Listing(tables)}");
        }

        private static string Listing(IReadOnlyList<ExcelTable> tables) =>
            tables.Count == 0
                ? "이 파일에는 표가 없어요. action create로 먼저 만들어 주세요."
                : "있는 표: " + string.Join(", ", tables.Select(t => $"{t.Name}({t.Sheet.Name}!{t.Range})"));

        private static DocuChange Change(ExcelSheet sheet, ExcelTable table)
        {
            var header = table.HasHeader ? table.HeaderRange : new CellRange(table.Range.Left, table.Range.Top, table.Range.Right, table.Range.Top);
            return new DocuChange
            {
                Location = $"{table.Name} {table.Range}",
                Icon = "table",
                Anchors = Anchors(sheet, header.Cells().Take(MaxAnchors)),
            };
        }

        private static void Finish(ExcelBook book, List<CellKey> written, bool reconcile = true)
        {
            if (written.Count > 0)
                Recalculate(book, written, false);
            else
                book.MarkCalculationNeeded();

            if (reconcile)
                ExcelTables.Reconcile(book);
        }

        private sealed class TableOptions
        {
            public bool? BandedRows { get; private init; }

            public bool? BandedColumns { get; private init; }

            public bool? FirstColumn { get; private init; }

            public bool? LastColumn { get; private init; }

            public bool? TotalRow { get; private init; }

            public bool? FilterButtons { get; private init; }

            public bool Any => BandedRows.HasValue || BandedColumns.HasValue || FirstColumn.HasValue || LastColumn.HasValue || TotalRow.HasValue || FilterButtons.HasValue;

            public static TableOptions Read(ToolInput input) => new()
            {
                BandedRows = input.GetOptionalBool("banded_rows"),
                BandedColumns = input.GetOptionalBool("banded_columns"),
                FirstColumn = input.GetOptionalBool("first_column"),
                LastColumn = input.GetOptionalBool("last_column"),
                TotalRow = input.GetOptionalBool("total_row"),
                FilterButtons = input.GetOptionalBool("filter_buttons"),
            };
        }
    }
}
