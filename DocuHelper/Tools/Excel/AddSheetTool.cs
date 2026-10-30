using System;
using System.Linq;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class AddSheetTool : ExcelTool
    {
        private static readonly char[] s_invalid = { ':', '\\', '/', '?', '*', '[', ']' };

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
            var name = input.GetString("name").Trim();
            if (name.Length == 0)
                throw new ToolInputException("새 시트 이름을 알려 주세요.");

            if (name.Length > 31)
                throw new ToolInputException("시트 이름은 31자까지만 쓸 수 있어요.");

            if (name.IndexOfAny(s_invalid) >= 0)
                throw new ToolInputException("시트 이름에는 : \\ / ? * [ ] 를 쓸 수 없어요.");

            if (name.StartsWith("'", StringComparison.Ordinal) || name.EndsWith("'", StringComparison.Ordinal))
                throw new ToolInputException("시트 이름은 작은따옴표(')로 시작하거나 끝날 수 없어요.");

            if (string.Equals(name, "History", StringComparison.OrdinalIgnoreCase))
                throw new ToolInputException("History는 엑셀이 따로 쓰는 이름이라 시트 이름으로 쓸 수 없어요.");

            if (book.Sheets.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new ToolInputException($"같은 이름의 시트가 벌써 있어요: ‘{name}’. 다른 이름을 골라 주세요.");

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
