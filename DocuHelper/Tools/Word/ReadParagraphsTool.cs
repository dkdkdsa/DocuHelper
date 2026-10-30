using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Word
{
    public class ReadParagraphsTool : WordTool
    {
        private const int DefaultCount = 60;
        private const int MaxCount = 200;
        private const int Budget = 8000;
        private const int ParagraphLimit = 3000;
        private const int CellLimit = 80;
        private const int ListedRows = 30;
        private const int FocusedRows = 200;

        public override string Name => "read_paragraphs";

        public override string Description =>
            "Read the Word document as numbered blocks (paragraphs and tables) in body order. Each line shows the block index, its [id], "
            + "kind (title, heading1, heading2, body, bullet, number, table), estimated page and text. Line breaks inside a paragraph appear as \\n "
            + "and tabs as \\t. Tables show their id, size and rows as \"a | b | c\". Use the ids with the other Word tools. "
            + "Output is capped at about 8000 characters; when more blocks follow, the result says which start to use next.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "start": {"type": "integer", "description": "1-based index of the first block to read. Default 1."},
                "count": {"type": "integer", "description": "How many blocks to read, 1-200. Default 60."}
              }
            }
            """;

        public override bool IsReadOnly => true;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var start = input.GetOptionalInt("start") ?? 1;
            var count = Math.Clamp(input.GetOptionalInt("count") ?? DefaultCount, 1, MaxCount);
            if (start < 1)
                start = 1;

            var scan = WordScan.Read(docu.Document);
            var blocks = scan.Blocks;
            if (blocks.Count == 0)
                return ToolResult.Ok("The document has no paragraphs yet. Add some with insert_paragraphs and after=\"start\".", "빈 문서");

            if (start > blocks.Count)
                throw new ToolInputException($"이 문서에는 블록이 {blocks.Count}개 있어요. start는 1~{blocks.Count} 사이로 넣어 주세요.");

            var body = new StringBuilder();
            var shown = new List<WordBlock>();
            for (var i = start - 1; i < blocks.Count && shown.Count < count; i++)
            {
                var entry = Format(blocks[i], shown.Count == 0);
                if (shown.Count > 0 && body.Length + entry.Length > Budget)
                    break;

                body.Append(entry);
                shown.Add(blocks[i]);
            }

            var last = shown[^1].Index;
            var text = new StringBuilder();
            text.Append(CultureInfo.InvariantCulture, $"Blocks {start}-{last} of {blocks.Count}, about {scan.PageCount} page(s); page numbers are estimates.\n");
            text.Append(body);
            if (last < blocks.Count)
                text.Append(CultureInfo.InvariantCulture, $"More blocks follow. Call read_paragraphs with start={last + 1} to continue.");

            var tables = shown.Count(b => b.IsTable);
            var paragraphs = shown.Count - tables;
            var detail = tables > 0 ? $"문단 {paragraphs}개, 표 {tables}개" : $"문단 {paragraphs}개";
            return ToolResult.Ok(text.ToString().TrimEnd(), detail);
        }

        private static string Format(WordBlock block, bool focused)
        {
            var line = new StringBuilder();
            line.Append(CultureInfo.InvariantCulture, $"{block.Index}. [{block.Id ?? "no id"}] ");
            if (block.IsTable)
            {
                AppendTable(line, block, focused);
                return line.ToString();
            }

            line.Append(KindName(block.Kind));
            if ((block.Kind == WordKind.Bullet || block.Kind == WordKind.Number) && block.ListLevel > 0)
                line.Append(CultureInfo.InvariantCulture, $" level {block.ListLevel + 1}");

            if (block.IsHeading && block.IsNumbered)
                line.Append(" auto-numbered");

            line.Append(CultureInfo.InvariantCulture, $", p{block.Page}: ");
            line.Append(block.IsEmpty && block.Text.Length == 0 ? "(empty)" : Escape(Shorten(block.Text, ParagraphLimit)));
            line.Append('\n');
            return line.ToString();
        }

        private static void AppendTable(StringBuilder line, WordBlock block, bool focused)
        {
            var grid = WordTables.Grid(block.Table);
            var rows = WordTables.Texts(grid);
            var columns = WordTables.ColumnCount(grid);
            line.Append(CultureInfo.InvariantCulture, $"table {rows.Count}x{columns}, p{block.Page}:\n");

            var limit = focused ? FocusedRows : ListedRows;
            var cellLimit = focused ? CellLimit * 4 : CellLimit;
            var written = 0;
            foreach (var row in rows)
            {
                if (written >= limit || (focused && line.Length > Budget))
                    break;

                line.Append("   ");
                line.Append(string.Join(" | ", row.Select(cell => Escape(Shorten(cell, cellLimit)))));
                line.Append('\n');
                written++;
            }

            if (written < rows.Count && focused)
                line.Append(CultureInfo.InvariantCulture, $"   … {rows.Count - written} more rows not shown.\n");
            else if (written < rows.Count)
                line.Append(CultureInfo.InvariantCulture, $"   … {rows.Count - written} more rows not shown. Call read_paragraphs with start={block.Index} and count=1 to see more.\n");
        }

        private static string Shorten(string text, int limit) =>
            text.Length <= limit ? text : text.Substring(0, limit) + $"… (+{text.Length - limit} chars)";

        private static string Escape(string text) => text.Replace("\n", "\\n").Replace("\t", "\\t");
    }
}
