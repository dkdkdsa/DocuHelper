using System.Linq;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class CopySheetTool : ExcelTool
    {
        public override string Name => "copy_sheet";

        public override string Description =>
            "Copy a whole sheet (values, formulas, formatting, merged cells, column widths, pictures and charts) into a new sheet, like Excel's 복사본 만들기. "
            + "new_name defaults to the old name with (2); position is the new tab position (1 = first) and defaults to right after the original.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet to copy."},
                "new_name": {"type": "string", "description": "Name of the copy, up to 31 characters."},
                "position": {"type": "integer", "description": "Tab position of the copy, 1 = first."}
              },
              "required": ["sheet"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var source = FindSheet(book, input);
            if (source.Part.PivotTableParts.Any() || source.Part.SlicersParts.Any() || source.Part.TimeLineParts.Any())
                throw new ToolInputException($"‘{source.Name}’ 시트에는 피벗 표나 슬라이서가 있어서 여기서는 복사할 수 없어요. 엑셀에서 해 주세요.");

            var newName = input.GetOptionalString("new_name");
            var name = string.IsNullOrWhiteSpace(newName) ? SheetNames.CopyName(book, source.Name) : SheetNames.Check(book, newName);
            var count = book.WorkbookPart.Workbook.Sheets.ChildElements.Count;
            var position = ParsePosition(input, count + 1) ?? book.WorkbookPart.Workbook.Sheets.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().ToList().IndexOf(source.Entry) + 1;
            var copy = book.CopySheet(source, name, position);
            book.MarkCalculationNeeded();

            var content = $"Copied ‘{source.Name}’ to the new sheet ‘{copy.Name}’ at tab {position + 1}. Sheets now: {book.SheetList()}.";
            var change = new DocuChange
            {
                Location = "새 시트",
                Icon = "copy",
                Anchors = { ExcelAnchor.Sheet(copy.Name) },
            };
            return ToolResult.Changed(content, change, "복사본");
        }
    }
}
