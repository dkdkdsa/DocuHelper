using System;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class FreezePanesTool : ExcelTool
    {
        public override string Name => "freeze_panes";

        public override string Description =>
            "Freeze the rows above and the columns to the left of `cell` so they stay in view while scrolling, like Excel's 틀 고정. "
            + "\"A2\" freezes the first row (a header), \"B1\" the first column, \"B2\" both, \"C4\" the first 3 rows and 2 columns. \"none\" unfreezes.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "cell": {"type": "string", "description": "The first cell that scrolls, e.g. \"A2\", or \"none\" to unfreeze."}
              },
              "required": ["sheet", "cell"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, "cell", true);
            var text = input.GetString("cell").Trim();
            var unfreeze = string.Equals(text, "none", StringComparison.OrdinalIgnoreCase) || text == "A1" || text == "$A$1";
            var cell = unfreeze ? new CellAddress(0, 0) : ParseCell(text);
            if (cell.Row > 1000 || cell.Column > 200)
                throw new ToolInputException("틀 고정은 1000행, 200열 안쪽에서만 할 수 있어요.");

            sheet.SetFreeze(cell.Row, cell.Column);
            string content;
            if (unfreeze)
            {
                content = $"Unfroze ‘{sheet.Name}’; every row and column scrolls now.";
            }
            else
            {
                var parts = new System.Collections.Generic.List<string>();
                if (cell.Row > 0)
                    parts.Add(cell.Row == 1 ? "the first row" : $"the first {cell.Row} rows");

                if (cell.Column > 0)
                    parts.Add(cell.Column == 1 ? "the first column" : $"the first {cell.Column} columns");

                content = $"Froze {string.Join(" and ", parts)} of ‘{sheet.Name}’; they stay in view while scrolling.";
            }

            var change = new DocuChange
            {
                Location = unfreeze ? "고정 풀기" : cell.ToString(),
                Icon = "freeze",
                Anchors = { ExcelAnchor.Cell(sheet.Name, cell) },
            };
            return ToolResult.Changed(content, change, unfreeze ? "고정 풀기" : "틀 고정");
        }
    }
}
