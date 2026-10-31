using System.Linq;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class DeleteRowsColumnsTool : ExcelTool
    {
        public override string Name => "delete_rows_columns";

        public override string Description =>
            "Delete whole rows or whole columns, like Excel's 삭제. Pass rows (\"5\" or \"5:7\") or columns (\"C\" or \"C:D\"), not both. Rows below move up and columns to the right move left. "
            + "Formulas, merged cells, conditional formatting, data validation, named ranges and charts in the whole workbook are adjusted the way Excel does it: =SUM(B2:B10) shrinks, "
            + "and a formula that pointed only at deleted cells shows #REF!. To empty cells without moving anything, write \"\" with write_cells instead.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "rows": {"type": "string", "description": "Rows to delete, e.g. \"5\" or \"5:7\"."},
                "columns": {"type": "string", "description": "Columns to delete, e.g. \"C\" or \"C:D\"."}
              },
              "required": ["sheet"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, null, true);
            var (rows, first, last) = ParseLines(input);
            var count = last - first + 1;
            var filled = sheet.Entries.Count(e => ExcelSheet.HasContent(e.Cell) && (rows ? e.Row : e.Column) >= first && (rows ? e.Row : e.Column) <= last);

            var shift = new LineShift(rows, first, -count);
            var outcome = ExcelStructure.ShiftLines(book, sheet, shift, ExcelStructure.FormatNone);
            ShiftAnchors(docu, sheet.Name, shift);
            var dirty = outcome.DirtyKeys(book);
            var result = Recalculate(book, dirty, false);
            var broken = dirty.Where(k => result.Values.TryGetValue(k, out var v) && v.IsError && v.Text == ExcelValue.BadReference).ToList();

            var moved = rows ? "rows below moved up" : "columns to the right moved left";
            var content = $"Deleted {LineText(rows, first, last)} of ‘{sheet.Name}’ ({filled} filled cell(s) removed); {moved} by {count}."
                + (outcome.Changed > 0 ? $" {outcome.Changed} formula(s) or references were adjusted." : string.Empty)
                + (broken.Count > 0 ? $" {broken.Count} formula cell(s) now show #REF! because they pointed at deleted cells, e.g. {broken[0].Address}." : string.Empty)
                + (result.Updated > 0 ? $" {result.Updated} formula cell(s) were recalculated." : string.Empty);

            var change = new DocuChange
            {
                Location = LineLabel(rows, first, last),
                Icon = "trash",
            };
            return ToolResult.Changed(content, change, $"{count}{(rows ? "행" : "열")} 지움");
        }
    }
}
