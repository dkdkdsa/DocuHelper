using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class InsertTableTool : WordTool
    {
        private const int MaxRows = 500;
        private const int MaxColumns = 20;

        public override string Name => "insert_table";

        public override string Description =>
            "Insert a table after a paragraph or table (or at the start or end of the document). rows is a list of rows, each a list of cell texts. "
            + "The table gets thin borders and fills the text width; the first row is a bold, shaded header that repeats on each page unless header is false. "
            + "Columns whose cells all look like numbers are right-aligned, so write numbers the way they should appear, e.g. \"14,820,000\". "
            + "Returns the new table's id; change single cells later with set_table_cell.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "after": {"type": "string", "description": "Id of the paragraph or table to insert after, or \"start\" or \"end\" of the document."},
                "rows": {
                  "type": "array",
                  "description": "Table rows from top to bottom, each a list of cell texts. All rows should have the same number of cells.",
                  "items": {"type": "array", "items": {"type": "string"}}
                },
                "header": {"type": "boolean", "description": "Whether the first row is a header row. Default true."}
              },
              "required": ["after", "rows"]
            }
            """;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var after = input.GetString("after");
            var rows = input.GetTable("rows")
                .Select(row => (IReadOnlyList<string>)row.Select(cell => WordText.Clean(ToolInput.AsText(cell))).ToList())
                .ToList();
            var header = input.GetOptionalBool("header") ?? true;

            if (rows.Count == 0)
                throw new ToolInputException("'rows'에 표의 줄을 하나 이상 넣어 주세요.");

            if (rows.Count > MaxRows)
                throw new ToolInputException($"표는 한 번에 {MaxRows}줄까지 넣을 수 있어요.");

            var columns = rows.Max(r => r.Count);
            if (columns == 0)
                throw new ToolInputException("표의 줄마다 칸을 하나 이상 넣어 주세요.");

            if (columns > MaxColumns)
                throw new ToolInputException($"표는 {MaxColumns}칸까지 넣을 수 있어요.");

            var scan = WordScan.Read(docu.Document);
            var insertion = WordInsertion.Resolve(scan, after);
            var used = WordIds.Collect(docu.Document);
            WordPager.TextArea(scan.Body, out var width, out _);

            var table = WordTables.Build(rows, header, width, used);
            insertion.Insert(table);

            if (table.PreviousSibling() is Table)
                table.InsertBeforeSelf(Spacer(used));

            var next = table.NextSibling();
            if (next == null || next is Table || next is SectionProperties)
                table.InsertAfterSelf(Spacer(used));

            var rescan = WordScan.Read(docu.Document);
            var block = rescan.Of(table);
            var id = block?.Id ?? WordTables.IdOf(table);

            return ToolResult.Changed(
                $"Inserted a {rows.Count}x{columns} table with id {id} {insertion.Describe}.",
                Change(WordScan.PageLabel(new[] { block }), "table", new[] { WordIds.TableAnchor(id) }),
                $"{rows.Count}행 {columns}열");
        }

        private static Paragraph Spacer(HashSet<string> used)
        {
            var paragraph = new Paragraph();
            WordIds.Assign(paragraph, used);
            return paragraph;
        }
    }
}
