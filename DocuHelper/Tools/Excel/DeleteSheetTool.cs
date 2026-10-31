using System.Linq;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class DeleteSheetTool : ExcelTool
    {
        public override string Name => "delete_sheet";

        public override string Description =>
            "Delete a whole sheet and everything on it. Formulas on other sheets that referred to it turn into #REF!, like in Excel. "
            + "The last visible sheet can't be deleted. Read the sheet first so you know what will be lost.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet to delete."}
              },
              "required": ["sheet"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = FindSheet(book, input);
            if (!sheet.IsHidden && book.VisibleSheetCount <= 1)
                throw new ToolInputException("보이는 시트가 하나뿐이라 지울 수 없어요. 통합 문서에는 보이는 시트가 하나 이상 있어야 해요.");

            if (sheet.Part.PivotTableParts.Any())
                throw new ToolInputException($"‘{sheet.Name}’ 시트에는 피벗 표가 있어서 여기서는 지울 수 없어요. 엑셀에서 해 주세요.");

            var name = sheet.Name;
            var filled = sheet.Entries.Count(e => ExcelSheet.HasContent(e.Cell));
            var outcome = book.DeleteSheet(sheet);
            DropSheetAnchors(docu, name);
            var dirty = outcome.DirtyKeys(book);
            var result = Recalculate(book, dirty, false);

            var content = $"Deleted the sheet ‘{name}’ ({filled} filled cell(s)). Sheets now: {book.SheetList()}."
                + (dirty.Count > 0 ? $" {dirty.Count} formula cell(s) on other sheets referred to it and now show #REF!, e.g. ‘{book.Sheets[dirty[0].SheetIndex].Name}’!{dirty[0].Address}." : string.Empty)
                + (outcome.Changed > dirty.Count ? " Named ranges or charts that used it now point at #REF!." : string.Empty)
                + (result.Updated > 0 ? $" {result.Updated} formula cell(s) were recalculated." : string.Empty);

            var change = new DocuChange
            {
                Location = $"‘{name}’ 시트",
                Icon = "trash",
            };
            return ToolResult.Changed(content, change, "시트 지움");
        }
    }
}
