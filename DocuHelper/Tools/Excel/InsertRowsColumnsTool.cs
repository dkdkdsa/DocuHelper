using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class InsertRowsColumnsTool : ExcelTool
    {
        private const int MaxRows = 5000;
        private const int MaxColumns = 200;

        public override string Name => "insert_rows_columns";

        public override string Description =>
            "Insert empty whole rows or whole columns, like Excel's 삽입. Pass rows or columns, not both. rows \"5\" inserts one row: the new empty row becomes row 5 and the old row 5 and everything below move down; "
            + "\"5:7\" inserts three rows (the new rows are 5-7). columns \"C\" or \"C:D\" work the same way to the right. "
            + "Formulas, merged cells, conditional formatting, data validation, named ranges and charts in the whole workbook are adjusted the way Excel does it, so =SUM(B2:B10) grows when rows are inserted inside it. "
            + "Use this to add rows inside a table instead of overwriting rows below it. The new cells take the formatting of the row above (or the column to the left) unless format_from says otherwise.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "rows": {"type": "string", "description": "Row numbers the new rows will have, e.g. \"5\" or \"5:7\"."},
                "columns": {"type": "string", "description": "Column letters the new columns will have, e.g. \"C\" or \"C:D\"."},
                "format_from": {"type": "string", "enum": ["above", "below", "left", "right", "none"], "description": "Which neighbor the new cells copy their formatting from. Default above for rows, left for columns."}
              },
              "required": ["sheet"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, null, true);
            var (rows, first, last) = ParseLines(input);
            var count = last - first + 1;
            var limit = rows ? MaxRows : MaxColumns;
            if (count > limit)
                throw new ToolInputException($"한 번에 {limit}{(rows ? "행" : "열")}까지만 넣을 수 있어요.");

            var format = (input.GetOptionalString("format_from")?.Trim().ToLowerInvariant()) switch
            {
                null or "" => ExcelStructure.FormatBefore,
                "above" when rows => ExcelStructure.FormatBefore,
                "below" when rows => ExcelStructure.FormatAfter,
                "left" when !rows => ExcelStructure.FormatBefore,
                "right" when !rows => ExcelStructure.FormatAfter,
                "none" => ExcelStructure.FormatNone,
                _ => throw new ToolInputException(rows ? "행을 넣을 때 format_from은 above, below, none 중 하나예요." : "열을 넣을 때 format_from은 left, right, none 중 하나예요."),
            };

            var shift = new LineShift(rows, first, count);
            var outcome = ExcelStructure.ShiftLines(book, sheet, shift, format);
            ShiftAnchors(docu, sheet.Name, shift);
            var result = Recalculate(book, outcome.DirtyKeys(book), false);

            var moved = rows ? "everything from that row down moved down" : "everything from that column on moved right";
            var content = $"Inserted {count} empty {(rows ? "row" : "column")}(s) in ‘{sheet.Name}’, now {LineText(rows, first, last)}; {moved} by {count}."
                + (outcome.Changed > 0 ? $" {outcome.Changed} formula(s) or references were adjusted." : string.Empty)
                + (result.Updated > 0 ? $" {result.Updated} formula cell(s) were recalculated." : string.Empty);

            var change = new DocuChange
            {
                Location = LineLabel(rows, first, last),
                Icon = rows ? "row-insert" : "column-insert",
                Anchors = LineAnchors(sheet, rows, first, last),
            };
            return ToolResult.Changed(content, change, $"{count}{(rows ? "행" : "열")} 넣음");
        }
    }
}
