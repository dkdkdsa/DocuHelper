using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class SetTableCellTool : WordTool
    {
        public override string Name => "set_table_cell";

        public override string Description =>
            "Replace the text of one table cell, keeping the cell's formatting. row and column are 1-based and count the header row; "
            + "a merged cell is reached from any row or column it covers. Use \\n for a line break inside the cell.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "table": {"type": "string", "description": "Id of the table, from read_paragraphs."},
                "row": {"type": "integer", "description": "1-based row number; row 1 is the top row."},
                "column": {"type": "integer", "description": "1-based column number; column 1 is the leftmost column."},
                "text": {"type": "string", "description": "The new text of the cell."}
              },
              "required": ["table", "row", "column", "text"]
            }
            """;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var id = input.GetString("table");
            var row = input.GetInt("row");
            var column = input.GetInt("column");
            if (!input.Has("text"))
                throw new ToolInputException("'text' 값이 필요해요.");

            var text = WordText.Clean(input.GetOptionalString("text"));
            var scan = WordScan.Read(docu.Document);
            var block = scan.Find(id) ?? throw NotFound(id);
            if (!block.IsTable)
                throw new ToolInputException($"‘{block.Id}’은 표가 아니에요. 문단은 replace_paragraph로 고쳐 주세요.");

            var grid = WordTables.Grid(block.Table);
            if (row < 1 || row > grid.Count)
                throw new ToolInputException($"이 표는 {grid.Count}줄이에요. row는 1~{grid.Count} 사이로 넣어 주세요.");

            var columns = grid[row - 1].Count;
            if (column < 1 || column > columns)
                throw new ToolInputException($"{row}번째 줄은 {columns}칸이에요. column은 1~{columns} 사이로 넣어 주세요.");

            var cell = WordTables.CellAt(grid, row - 1, column - 1);
            if (cell == null)
                throw new ToolInputException("그 자리는 비어 있는 칸이라 글을 넣을 수 없어요.");

            var paragraph = cell.Elements<Paragraph>().FirstOrDefault();
            if (paragraph == null)
            {
                paragraph = new Paragraph();
                WordIds.Assign(paragraph, WordIds.Collect(docu.Document));
                cell.AppendChild(paragraph);
            }

            foreach (var extra in cell.ChildElements.Where(e => e is not TableCellProperties && !ReferenceEquals(e, paragraph)).ToList())
                extra.Remove();

            WordText.Write(paragraph, text);

            var updated = WordScan.Read(docu.Document).Of(block.Table) ?? block;
            var location = WordScan.PageLabel(new[] { updated });
            return ToolResult.Changed(
                $"Set row {row}, column {column} of table {updated.Id}.",
                Change(location, "table", new[] { updated.Anchor }),
                $"{row}행 {column}열");
        }
    }
}
