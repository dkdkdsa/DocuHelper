using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocuHelper.Importers.Hwp;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class EditTableTool : WordTool
    {
        private const int MinColumnWidth = 200;

        public override string Name => "edit_table";

        public override string Description =>
            "Change the structure and look of an existing table: insert or delete rows and columns, merge or split cells, set column widths or make them equal, "
            + "shade or border cells, align cell content, and set table options (header row, banded rows, automatic fit). "
            + "Rows and columns are 1-based and count the header row; a merged cell covers all the rows and columns it spans. "
            + "Hangul files keep structure, widths, shading, borders, row heights, vertical alignment and the header row, but not presets, banded or total rows, table alignment, width or automatic fit.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "table": {"type": "string", "description": "Table id from read_paragraphs."},
                "action": {"type": "string", "enum": ["insert_rows", "insert_columns", "delete_rows", "delete_columns", "delete_table", "merge_cells", "split_cell", "set_widths", "distribute", "format", "set_style"]},
                "row": {"type": "integer", "minimum": 1, "description": "insert_rows/delete_rows: reference row (insert default: last row for after, first for before). split_cell: the cell's row."},
                "column": {"type": "integer", "minimum": 1, "description": "insert_columns/delete_columns: reference column. split_cell: the cell's column."},
                "count": {"type": "integer", "minimum": 1, "maximum": 100, "description": "How many rows or columns to insert or delete. Default 1."},
                "position": {"type": "string", "enum": ["before", "after"], "description": "Insert before or after the reference row or column. Default after."},
                "range": {
                  "type": "object",
                  "properties": {
                    "row1": {"type": "integer", "minimum": 1},
                    "column1": {"type": "integer", "minimum": 1},
                    "row2": {"type": "integer", "minimum": 1},
                    "column2": {"type": "integer", "minimum": 1}
                  },
                  "description": "Cells for merge_cells, distribute and format; omit for the whole table (required for merge_cells). row2 and column2 default to row1 and column1."
                },
                "widths": {"type": "array", "items": {"type": "number"}, "description": "set_widths: percentages of the table width, one per column."},
                "axis": {"type": "string", "enum": ["rows", "columns"], "description": "distribute."},
                "split": {
                  "type": "object",
                  "properties": {
                    "rows": {"type": "integer", "minimum": 1, "maximum": 20},
                    "columns": {"type": "integer", "minimum": 1, "maximum": 20}
                  },
                  "description": "split_cell: pieces to divide the cell into. Omit to undo a merge."
                },
                "format": {
                  "type": "object",
                  "properties": {
                    "shading": {"type": "string", "description": "#RRGGBB or \"none\"."},
                    "h_align": {"type": "string", "enum": ["left", "center", "right"]},
                    "v_align": {"type": "string", "enum": ["top", "center", "bottom"]},
                    "bold": {"type": "boolean"},
                    "color": {"type": "string", "description": "Text color #RRGGBB."},
                    "size": {"type": "number", "description": "Text size in points."},
                    "borders": {
                      "type": "object",
                      "properties": {
                        "sides": {"type": "array", "items": {"type": "string", "enum": ["top", "bottom", "left", "right", "inside_h", "inside_v", "outer", "all"]}},
                        "style": {"type": "string", "enum": ["single", "double", "dotted", "dashed", "thick", "none"]},
                        "width_pt": {"type": "number"},
                        "color": {"type": "string"}
                      }
                    }
                  }
                },
                "style": {
                  "type": "object",
                  "properties": {
                    "preset": {"type": "string", "enum": ["grid", "plain", "banded", "horizontal"]},
                    "header_row": {"type": "boolean"},
                    "banded_rows": {"type": "boolean"},
                    "total_row": {"type": "boolean"},
                    "alignment": {"type": "string", "enum": ["left", "center", "right"]},
                    "width_percent": {"type": "number", "minimum": 10, "maximum": 100},
                    "autofit": {"type": "string", "enum": ["contents", "window", "fixed"]}
                  }
                }
              },
              "required": ["table", "action"]
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.Partial;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var action = input.GetString("action").Trim().ToLowerInvariant();
            var scan = WordScan.Read(docu.Document);
            var id = input.GetString("table");
            var block = scan.Find(id) ?? throw new ToolInputException($"‘{WordIds.Normalize(id) ?? id}’ 표를 찾지 못했어요. read_paragraphs로 표 id를 다시 확인해 주세요.");
            if (!block.IsTable)
                throw new ToolInputException($"‘{block.Id}’은 표가 아니에요. 문단은 replace_paragraph나 format_paragraph로 고쳐 주세요.");

            var session = new Session(docu, scan, block, input);
            switch (action)
            {
                case "insert_rows":
                    return InsertRows(session);
                case "insert_columns":
                    return InsertColumns(session);
                case "delete_rows":
                    return DeleteRows(session);
                case "delete_columns":
                    return DeleteColumns(session);
                case "delete_table":
                    return DeleteTable(session);
                case "merge_cells":
                    return MergeCells(session);
                case "split_cell":
                    return SplitCell(session);
                case "set_widths":
                    return SetWidths(session);
                case "distribute":
                    return Distribute(session);
                case "format":
                    return Format(session);
                case "set_style":
                    return SetStyle(session);
                default:
                    throw new ToolInputException("'action'은 insert_rows, insert_columns, delete_rows, delete_columns, delete_table, merge_cells, split_cell, set_widths, distribute, format, set_style 중 하나여야 해요.");
            }
        }

        private static ToolResult InsertRows(Session s)
        {
            var count = s.Count();
            var after = s.Position();
            var reference = s.Input.Has("row") ? s.Row("row") : after ? s.Rows - 1 : 0;
            if (s.Rows + count > WordTableOps.MaxRows)
                throw new ToolInputException($"표는 {WordTableOps.MaxRows}줄까지 만들 수 있어요.");

            var at = after ? reference + 1 : reference;
            var where = after ? $"after row {reference + 1}" : $"before row {reference + 1}";
            return s.Commit(
                table =>
                {
                    WordTableOps.InsertRows(table, at, reference, count, false, s.Used);
                    WordTableStyles.Reband(s.Scan.Styles, table);
                },
                $"Inserted {count} row(s) {where}.",
                $"행 {count}개 넣음");
        }

        private static ToolResult InsertColumns(Session s)
        {
            s.RequireRegular("열을 넣을 수");
            var count = s.Count();
            var after = s.Position();
            var reference = s.Input.Has("column") ? s.Column("column") : after ? s.Columns - 1 : 0;
            if (s.Columns + count > WordTableOps.MaxColumns)
                throw new ToolInputException($"표는 {WordTableOps.MaxColumns}칸까지 만들 수 있어요.");

            var line = after ? reference + 1 : reference;
            var where = after ? $"after column {reference + 1}" : $"before column {reference + 1}";
            return s.Commit(
                table => WordTableOps.InsertColumns(table, line, reference, count, s.Used),
                $"Inserted {count} column(s) {where}.",
                $"열 {count}개 넣음");
        }

        private static ToolResult DeleteRows(Session s)
        {
            var first = s.Row("row");
            var count = s.Count();
            var last = first + count - 1;
            if (last >= s.Rows)
                throw new ToolInputException($"{first + 1}번째 줄부터 {count}줄은 지울 수 없어요. 이 표는 {s.Rows}줄이에요.");

            if (count >= s.Rows)
                throw new ToolInputException("표의 모든 줄을 지울 수는 없어요. 표를 통째로 지우려면 action을 delete_table로 해 주세요.");

            return s.Commit(
                table =>
                {
                    WordTableOps.DeleteRows(table, first, last);
                    WordTableStyles.Reband(s.Scan.Styles, table);
                },
                $"Deleted {count} row(s) starting at row {first + 1}.",
                $"행 {count}개 지움");
        }

        private static ToolResult DeleteColumns(Session s)
        {
            s.RequireRegular("열을 지울 수");
            var first = s.Column("column");
            var count = s.Count();
            var last = first + count - 1;
            if (last >= s.Columns)
                throw new ToolInputException($"{first + 1}번째 열부터 {count}열은 지울 수 없어요. 이 표는 {s.Columns}칸이에요.");

            if (count >= s.Columns)
                throw new ToolInputException("표의 모든 열을 지울 수는 없어요. 표를 통째로 지우려면 action을 delete_table로 해 주세요.");

            return s.Commit(
                table => WordTableOps.DeleteColumns(table, first, last),
                $"Deleted {count} column(s) starting at column {first + 1}.",
                $"열 {count}개 지움");
        }

        private static ToolResult DeleteTable(Session s)
        {
            var summary = $"Deleted table {s.Block.Anchor} ({s.Rows} rows x {s.Columns} columns).";
            var location = WordScan.PageLabel(new[] { s.Block });
            var anchor = s.Block.Anchor;
            var table = s.Block.Table;
            var parent = table.Parent;
            var previous = table.PreviousSibling();
            var next = table.NextSibling();
            table.Remove();
            if (previous is Table && next is Table)
                next.InsertBeforeSelf(s.Spacer());

            if (parent is SdtContentBlock content && !WordScan.BlockElements(content).Any())
                content.Parent?.Remove();

            var body = s.Scan.Body;
            var blocks = body.ChildElements.Where(e => e is Paragraph || e is Table || e is SdtBlock || e is CustomXmlBlock).ToList();
            if (blocks.Count == 0 || blocks[^1] is Table)
            {
                var section = body.Elements<SectionProperties>().LastOrDefault();
                if (section != null)
                    section.InsertBeforeSelf(s.Spacer());
                else
                    body.AppendChild(s.Spacer());
            }

            return ToolResult.Changed(summary + s.TrackingNote(), Change(location, "trash", new[] { anchor }), "표 지움");
        }

        private static ToolResult MergeCells(Session s)
        {
            s.RequireRegular("칸을 합칠 수");
            if (!s.Input.Has("range"))
                throw new ToolInputException("'range'에 합칠 칸의 범위(row1, column1, row2, column2)를 넣어 주세요.");

            var range = s.Range();
            if (range.IsSingle)
                throw new ToolInputException("칸을 둘 이상 골라 주세요.");

            var extents = WordTableOps.Extents(s.Grid);
            var wide = WordTableOps.Expand(s.Grid, range);
            if (extents.ContainsValue(wide))
                throw new ToolInputException("이미 합쳐진 칸이에요.");

            if (wide != range)
                throw new ToolInputException($"고른 범위가 이미 합쳐진 칸을 일부만 걸치고 있어요. {wide.Row1 + 1}~{wide.Row2 + 1}행, {wide.Column1 + 1}~{wide.Column2 + 1}열로 넓혀서 다시 해 주세요.");

            return s.Commit(
                table => WordTableOps.Merge(table, range, s.Used),
                $"Merged rows {range.Row1 + 1}-{range.Row2 + 1}, columns {range.Column1 + 1}-{range.Column2 + 1} into one cell.",
                "칸 합침");
        }

        private static ToolResult SplitCell(Session s)
        {
            s.RequireRegular("칸을 나눌 수");
            var row = s.Row("row");
            var column = s.Column("column");
            var extent = WordTableOps.Extents(s.Grid)[s.Grid[row][column].Owner];
            var rows = 1;
            var columns = 1;
            if (s.Input.Has("split"))
            {
                var split = s.Input.Get("split");
                if (split.ValueKind != JsonValueKind.Object)
                    throw new ToolInputException("'split'은 rows와 columns를 담은 값이어야 해요.");

                rows = Pieces(split, "rows");
                columns = Pieces(split, "columns");
            }

            if (rows == 1 && columns == 1)
            {
                if (extent.IsSingle)
                    throw new ToolInputException("이 칸은 합쳐진 칸이 아니에요. split에 rows나 columns를 2 이상으로 넣어 주세요.");

                rows = extent.Rows;
                columns = extent.Columns;
            }

            var addedColumns = Math.Max(0, columns - extent.Columns);
            var addedRows = Math.Max(0, rows - extent.Rows);
            if (s.Columns + addedColumns > WordTableOps.MaxColumns)
                throw new ToolInputException($"표는 {WordTableOps.MaxColumns}칸까지 만들 수 있어요.");

            if (s.Rows + addedRows > WordTableOps.MaxRows)
                throw new ToolInputException($"표는 {WordTableOps.MaxRows}줄까지 만들 수 있어요.");

            return s.Commit(
                table => WordTableOps.Split(table, extent, rows, columns, s.Used),
                $"Split the cell at row {row + 1}, column {column + 1} into {rows} row(s) x {columns} column(s).",
                "칸 나눔");
        }

        private static ToolResult SetWidths(Session s)
        {
            s.RequireRegular("열 너비를 바꿀 수");
            if (!s.Input.Has("widths"))
                throw new ToolInputException("'widths'에 열마다 하나씩 퍼센트 숫자를 넣어 주세요.");

            var element = s.Input.Get("widths");
            if (element.ValueKind != JsonValueKind.Array)
                throw new ToolInputException("'widths'는 열마다 하나씩 퍼센트 숫자를 담은 목록이어야 해요.");

            var weights = new List<double>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var value) || value <= 0 || value > 100)
                    throw new ToolInputException("'widths'의 값은 0보다 크고 100 이하인 퍼센트 숫자여야 해요.");

                weights.Add(value);
            }

            if (weights.Count != s.Columns)
                throw new ToolInputException($"이 표는 {s.Columns}열이에요. 'widths'에 {s.Columns}개를 넣어 주세요.");

            var widths = WordTableOps.Fit(weights, WordTableOps.GridWidths(s.Block.Table).Sum());
            if (widths.Any(w => w < MinColumnWidth))
                throw new ToolInputException("너무 좁은 열이 있어요. 열마다 폭을 조금 더 넓게 넣어 주세요.");

            return s.Commit(
                table => WordTableOps.FixWidth(table, widths),
                $"Set the column widths to {string.Join(", ", weights.Select(w => w.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)))} percent.",
                "열 너비 바꿈");
        }

        private static ToolResult Distribute(Session s)
        {
            var axis = (s.Input.GetString("axis") ?? string.Empty).Trim().ToLowerInvariant();
            if (axis != "rows" && axis != "columns")
                throw new ToolInputException("'axis'는 rows 또는 columns여야 해요.");

            var range = s.Range();
            if (axis == "columns")
            {
                s.RequireRegular("열 너비를 같게 할 수");
                if (range.Columns < 2)
                    throw new ToolInputException("열을 둘 이상 골라 주세요.");

                return s.Commit(
                    table => WordTableOps.DistributeColumns(table, range.Column1, range.Column2),
                    $"Made columns {range.Column1 + 1}-{range.Column2 + 1} the same width.",
                    "열 너비 같게");
            }

            HwpGuard.Require(s.Docu, HwpFeature.TableCellFormat, "표 행 높이");
            if (range.Rows < 2)
                throw new ToolInputException("줄을 둘 이상 골라 주세요.");

            return s.Commit(
                table => WordTableOps.DistributeRows(s.Scan, table, range.Row1, range.Row2),
                $"Made rows {range.Row1 + 1}-{range.Row2 + 1} the same height (an estimate; Word grows a row to fit its text).",
                "행 높이 같게");
        }

        private static ToolResult Format(Session s)
        {
            if (!s.Input.Has("format"))
                throw new ToolInputException("'format'에 바꿀 서식(shading, h_align, v_align, bold, color, size, borders)을 넣어 주세요.");

            var format = WordCellFormat.Read(s.Input.Get("format"));
            if (format.Borders != null)
                HwpGuard.Require(s.Docu, HwpFeature.TableCellFormat, "표 테두리");

            var range = s.Range();
            return s.Commit(
                table => WordTableFormat.Apply(s.Scan, table, WordTables.Grid(table), range, format),
                $"Formatted rows {range.Row1 + 1}-{range.Row2 + 1}, columns {range.Column1 + 1}-{range.Column2 + 1}.",
                "표 서식");
        }

        private static ToolResult SetStyle(Session s)
        {
            if (!s.Input.Has("style"))
                throw new ToolInputException("'style'에 바꿀 표 옵션(preset, header_row, banded_rows, total_row, alignment, width_percent, autofit)을 넣어 주세요.");

            var style = WordTableStyle.Read(s.Input.Get("style"));
            if (style.Preset != null || style.BandedRows != null || style.TotalRow != null || style.Alignment != null || style.WidthPercent != null || style.Autofit != null)
                HwpGuard.Refuse(s.Docu, "표 스타일");
            if (style.TotalRow != null && s.Rows < 2)
                throw new ToolInputException("줄이 하나뿐인 표에는 합계 줄을 만들 수 없어요.");

            var regular = s.Regular;
            return s.Commit(
                table => WordTableStyles.Apply(s.Scan, table, style, regular),
                "Updated the table style.",
                "표 스타일");
        }

        private static int Pieces(JsonElement split, string name)
        {
            if (!split.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
                return 1;

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var count) || count < 1 || count > 20)
                throw new ToolInputException($"'split'의 {name}는 1~20 사이의 정수여야 해요.");

            return count;
        }

        private sealed class Session
        {
            public Session(WordDocu docu, WordScan scan, WordBlock block, ToolInput input)
            {
                Docu = docu;
                Scan = scan;
                Block = block;
                Input = input;
                Grid = WordTables.Grid(block.Table);
                Rows = Grid.Count;
                Columns = WordTables.ColumnCount(Grid);
                Regular = WordTableOps.IsRegular(block.Table, Grid);
                Used = WordIds.Collect(docu.Document);
            }

            public WordDocu Docu { get; }

            public WordScan Scan { get; }

            public WordBlock Block { get; }

            public ToolInput Input { get; }

            public List<List<WordCell>> Grid { get; }

            public int Rows { get; }

            public int Columns { get; }

            public bool Regular { get; }

            public HashSet<string> Used { get; }

            public void RequireRegular(string what)
            {
                if (!Regular)
                    throw new ToolInputException($"이 표는 줄마다 칸 구성이 달라서 {what} 없어요. 줄 단위 편집이나 서식은 쓸 수 있어요.");
            }

            public int Count()
            {
                var count = Input.GetOptionalInt("count") ?? 1;
                if (count < 1 || count > WordTableOps.MaxBatch)
                    throw new ToolInputException($"'count'는 1~{WordTableOps.MaxBatch} 사이로 넣어 주세요.");

                return count;
            }

            public bool Position()
            {
                var value = Input.GetOptionalString("position");
                if (string.IsNullOrWhiteSpace(value))
                    return true;

                return value.Trim().ToLowerInvariant() switch
                {
                    "after" => true,
                    "before" => false,
                    _ => throw new ToolInputException("'position'은 before 또는 after여야 해요."),
                };
            }

            public int Row(string name)
            {
                var row = Input.GetInt(name);
                if (row < 1 || row > Rows)
                    throw new ToolInputException($"이 표는 {Rows}줄이에요. '{name}'은 1~{Rows} 사이로 넣어 주세요.");

                return row - 1;
            }

            public int Column(string name)
            {
                var column = Input.GetInt(name);
                if (column < 1 || column > Columns)
                    throw new ToolInputException($"이 표는 {Columns}칸이에요. '{name}'은 1~{Columns} 사이로 넣어 주세요.");

                return column - 1;
            }

            public WordTableRange Range()
            {
                if (!Input.Has("range"))
                    return new WordTableRange(0, 0, Rows - 1, Columns - 1);

                var element = Input.Get("range");
                if (element.ValueKind != JsonValueKind.Object)
                    throw new ToolInputException("'range'는 row1, column1, row2, column2를 담은 값이어야 해요.");

                var row1 = Bound(element, "row1", null, Rows);
                var column1 = Bound(element, "column1", null, Columns);
                var row2 = Bound(element, "row2", row1, Rows);
                var column2 = Bound(element, "column2", column1, Columns);
                return new WordTableRange(Math.Min(row1, row2), Math.Min(column1, column2), Math.Max(row1, row2), Math.Max(column1, column2));
            }

            public Paragraph Spacer()
            {
                var paragraph = new Paragraph();
                WordIds.Assign(paragraph, Used);
                return paragraph;
            }

            public string TrackingNote() =>
                WordRevisions.Active(Docu) == null ? string.Empty : " Track changes is on, but table structure and format changes are not recorded as revisions.";

            public ToolResult Commit(Action<Table> change, string summary, string detail)
            {
                var original = Block.Table;
                var id = Block.Id;
                var copy = (Table)original.CloneNode(true);
                change(copy);
                WordTableOps.KeepId(copy, id);
                original.InsertBeforeSelf(copy);
                original.Remove();

                var updated = WordScan.Read(Docu.Document).Of(copy) ?? Block;
                var grid = WordTables.Grid(copy);
                var content = $"{summary} Table {updated.Anchor} now has {grid.Count} rows x {WordTables.ColumnCount(grid)} columns.{TrackingNote()}";
                return ToolResult.Changed(content, Change(WordScan.PageLabel(new[] { updated }), "table", new[] { updated.Anchor }), detail);
            }

            private int Bound(JsonElement element, string name, int? fallback, int limit)
            {
                if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
                {
                    if (fallback != null)
                        return fallback.Value;

                    throw new ToolInputException($"'range'에 {name}도 넣어 주세요.");
                }

                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
                    throw new ToolInputException($"'range'의 {name}은 정수여야 해요.");

                if (number < 1 || number > limit)
                    throw new ToolInputException($"'range'가 표 밖이에요. 이 표는 {Rows}줄 {Columns}칸이에요. {name}은 1~{limit} 사이로 넣어 주세요.");

                return number - 1;
            }
        }
    }
}
