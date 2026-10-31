using System.Linq;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class ShiftCellsTool : ExcelTool
    {
        public override string Name => "shift_cells";

        public override string Description =>
            "Insert or delete a block of cells and shift the cells around it, like Excel 셀 삽입 / 셀 삭제 with \"밀기\". "
            + "action insert with shift right|down opens empty cells and moves existing ones; action delete with shift left|up removes the cells and pulls the neighbours in. "
            + "Formulas, merged cells, conditional formatting, data validation, hyperlinks, names and charts are adjusted the way Excel does it; references to deleted cells become #REF!. "
            + "For whole rows or columns use insert_rows_columns / delete_rows_columns. Refuses when the block cuts through a merged cell, an array formula, a table or a filter range.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "range": {"type": "string", "description": "The cells to insert or delete, e.g. \"B5:D7\"."},
                "action": {"type": "string", "enum": ["insert", "delete"], "description": "insert or delete."},
                "shift": {"type": "string", "enum": ["right", "down", "left", "up"], "description": "Direction the neighbouring cells move. insert: right or down. delete: left or up. Default down for insert, up for delete."}
              },
              "required": ["sheet", "range", "action"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "range", true);
            var range = ParseRange(sheet, input.GetString("range"));
            var insert = ParseAction(input.GetString("action"));
            var direction = ParseDirection(input.GetOptionalString("shift"), insert);
            if (range.CellCount > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 {(insert ? "넣을" : "지울")} 수 있어요.");

            var shift = new CellShift(range, insert, direction is "down" or "up");
            CellShiftStructure.Check(book, sheet, shift);

            var filled = sheet.Entries.Count(e => ExcelSheet.HasContent(e.Cell) && range.Contains(e.Column, e.Row));
            var outcome = CellShiftStructure.Apply(book, sheet, shift);
            RemapAnchors(docu, sheet.Name, shift);
            var dirty = outcome.DirtyKeys(book);
            var result = Recalculate(book, dirty, false);
            var broken = dirty.Where(k => result.Values.TryGetValue(k, out var v) && v.IsError && v.Text == ExcelValue.BadReference).ToList();

            var moved = shift.Vertical ? "below" : "to the right";
            var content = insert
                ? $"Inserted {range.CellCount} empty cell(s) at ‘{sheet.Name}’ {range}; the existing cells {moved} moved {direction} by {shift.Size}."
                : $"Deleted ‘{sheet.Name}’ {range} ({filled} filled cell(s) removed); the cells {moved} moved {direction} by {shift.Size}.";
            content += (outcome.Changed > 0 ? $" {outcome.Changed} formula(s) or references were adjusted." : string.Empty)
                + (broken.Count > 0 ? $" {broken.Count} formula cell(s) now show #REF! because they pointed at deleted cells, e.g. {broken[0].Address}." : string.Empty)
                + (result.Updated > 0 ? $" {result.Updated} formula cell(s) were recalculated." : string.Empty);

            var change = new DocuChange
            {
                Location = $"{range} {Label(direction)}",
                Icon = insert ? "cins" : "cdel",
                Anchors = Anchors(sheet, range.Cells().Take(500)),
            };
            return ToolResult.Changed(content, change, $"{range.CellCount}칸 {(insert ? "넣음" : "지움")}");
        }

        private static bool ParseAction(string text)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "insert":
                    return true;
                case "delete":
                    return false;
                default:
                    throw new ToolInputException($"action은 insert(넣기)나 delete(지우기) 중 하나예요. 받은 값: {text}");
            }
        }

        private static string ParseDirection(string text, bool insert)
        {
            var value = text?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(value))
                return insert ? "down" : "up";

            if (value is "entire_row" or "entire_column")
                throw new ToolInputException("행이나 열 전체는 insert_rows_columns나 delete_rows_columns로 넣고 지워 주세요.");

            if (insert && value is "right" or "down")
                return value;

            if (!insert && value is "left" or "up")
                return value;

            throw new ToolInputException(insert
                ? $"셀을 넣을 때 shift는 right(오른쪽으로 밀기)나 down(아래로 밀기) 중 하나예요. 받은 값: {text}"
                : $"셀을 지울 때 shift는 left(왼쪽으로 당기기)나 up(위로 당기기) 중 하나예요. 받은 값: {text}");
        }

        private static string Label(string direction) => direction switch
        {
            "right" => "오른쪽으로 밀기",
            "down" => "아래로 밀기",
            "left" => "왼쪽으로 당기기",
            _ => "위로 당기기",
        };

        private static void RemapAnchors(Docu docu, string sheetName, CellShift shift)
        {
            foreach (var change in docu.Changes)
            {
                for (var i = change.Anchors.Count - 1; i >= 0; i--)
                {
                    if (!ExcelAnchor.TryParse(change.Anchors[i], out var sheet, out var cellRef) || sheet != sheetName || !CellAddress.TryParse(cellRef, out var address))
                        continue;

                    var mapped = shift.Map(address);
                    if (mapped == null)
                        change.Anchors.RemoveAt(i);
                    else
                        change.Anchors[i] = ExcelAnchor.Cell(sheetName, mapped.Value);
                }
            }
        }
    }
}
