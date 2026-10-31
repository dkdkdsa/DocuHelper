using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class InsertTableTool : WordTool
    {
        private const int MaxRows = 500;
        private const int MaxColumns = 20;
        private const int MinColumnWidth = 200;

        public override string Name => "insert_table";

        public override string Description =>
            "Insert a table after a paragraph or table (or at the start or end of the document). rows is a list of rows, each a list of cell texts. "
            + "By default the table gets thin borders and fills the text width; the first row is a bold, shaded header that repeats on each page unless header is false. "
            + "Columns whose cells all look like numbers are right-aligned, so write numbers the way they should appear, e.g. \"14,820,000\". "
            + "column_widths (percent per column), style (grid, plain, banded, horizontal) and align are optional. "
            + "Returns the new table's id; change cells later with set_table_cell and structure with edit_table.";

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
                "header": {"type": "boolean", "description": "Whether the first row is a header row. Default true."},
                "column_widths": {"type": "array", "items": {"type": "number"}, "description": "Percent of the text width per column (sum about 100)."},
                "style": {"type": "string", "enum": ["grid", "plain", "banded", "horizontal"], "description": "Default grid."},
                "align": {"type": "string", "enum": ["left", "center", "right"]}
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
            var style = input.Has("style") ? WordTableStyle.ParsePreset(input.GetOptionalString("style"), "style") : null;
            TableRowAlignmentValues? align = input.Has("align") ? WordTableStyle.ParseAlignment(input.GetOptionalString("align"), "align") : null;

            if (rows.Count == 0)
                throw new ToolInputException("'rows'에 표의 줄을 하나 이상 넣어 주세요.");

            if (rows.Count > MaxRows)
                throw new ToolInputException($"표는 한 번에 {MaxRows}줄까지 넣을 수 있어요.");

            var columns = rows.Max(r => r.Count);
            if (columns == 0)
                throw new ToolInputException("표의 줄마다 칸을 하나 이상 넣어 주세요.");

            if (columns > MaxColumns)
                throw new ToolInputException($"표는 {MaxColumns}칸까지 넣을 수 있어요.");

            var widths = ReadWidths(input, columns);
            var scan = WordScan.Read(docu.Document);
            var insertion = WordInsertion.Resolve(scan, after);
            var used = WordIds.Collect(docu.Document);
            WordPager.TextArea(scan.Body, out var width, out _);
            if (widths != null && WordTableOps.Fit(widths, width).Any(w => w < MinColumnWidth))
                throw new ToolInputException("'column_widths'에 너무 좁은 열이 있어요. 열마다 3% 이상으로 넣어 주세요.");

            var table = WordTables.Build(rows, header, width, used, widths, style, align);
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

        private static IReadOnlyList<double> ReadWidths(ToolInput input, int columns)
        {
            if (!input.Has("column_widths"))
                return null;

            var element = input.Get("column_widths");
            if (element.ValueKind != JsonValueKind.Array)
                throw new ToolInputException("'column_widths'는 열마다 하나씩 퍼센트 숫자를 담은 목록이어야 해요.");

            var widths = new List<double>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var value) || value < 1 || value > 100)
                    throw new ToolInputException("'column_widths'의 값은 1~100 사이의 퍼센트 숫자여야 해요.");

                widths.Add(value);
            }

            if (widths.Count != columns)
                throw new ToolInputException($"표가 {columns}열이라 'column_widths'에도 {columns}개를 넣어 주세요.");

            return widths;
        }

        private static Paragraph Spacer(HashSet<string> used)
        {
            var paragraph = new Paragraph();
            WordIds.Assign(paragraph, used);
            return paragraph;
        }
    }
}
