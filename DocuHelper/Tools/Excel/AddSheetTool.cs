using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class AddSheetTool : ExcelTool
    {
        public override string Name => "add_sheet";

        public override string Description =>
            "Add a new empty sheet to the workbook, at the end or right after another sheet. Fill it afterwards with write_cells and set_formula.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "name": {"type": "string", "description": "New sheet name, up to 31 characters, without : \\ / ? * [ ]. E.g. \"거래처별 요약\"."},
                "after": {"type": "string", "description": "Put the new sheet right after this sheet. Defaults to the end."}
              },
              "required": ["name"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var name = SheetNames.Check(book, input.GetString("name"));
            ExcelSheet after = null;
            var afterName = input.GetOptionalString("after");
            if (!string.IsNullOrWhiteSpace(afterName))
            {
                after = book.FindSheet(afterName);
                if (after == null)
                    throw new ToolInputException($"‘{afterName.Trim()}’ 시트를 찾을 수 없어요. 있는 시트: {book.SheetList()}");
            }

            var sheet = book.AddSheet(name, after);
            var place = after == null ? "at the end" : $"after ‘{after.Name}’";
            var content = $"Added the empty sheet ‘{sheet.Name}’ {place}. Sheets now: {book.SheetList()}.";
            var change = new DocuChange
            {
                Location = "새 시트",
                Icon = "file-plus",
                Anchors = { ExcelAnchor.Sheet(sheet.Name) },
            };
            return ToolResult.Changed(content, change, "새 시트");
        }
    }
}
