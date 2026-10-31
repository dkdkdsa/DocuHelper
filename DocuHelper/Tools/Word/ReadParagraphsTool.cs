using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

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
        private const int MaxIds = 200;

        public override string Name => "read_paragraphs";

        public override string Description =>
            "Read the Word document as numbered blocks (paragraphs and tables) in body order. Each line shows the block index, its [id], "
            + "kind (title, heading1, heading2, body, bullet, number, table), estimated page and text. Line breaks inside a paragraph appear as \\n "
            + "and tabs as \\t. Tables show their id, size and rows as \"a | b | c\". Use the ids with the other Word tools. "
            + "detail=\"format\" adds the look of each paragraph (style, alignment, spacing, indents, base font) and marks the characters that differ from the paragraph's base look inline: "
            + "{b}..{/b}, {!b} (turned off), {i}, {u}, {s}, {size:14}, {color:C0392B}, {hl:yellow}, {font:바탕}, {sup}, {sub}, {ins:author} for tracked insertions; a literal { is shown as \\{. "
            + "find lists only the blocks that contain a phrase, with the character offsets of each match (the offsets edit_text and format_text accept; in a table cell, offsets count the cell's paragraphs joined by \\n). "
            + "ids reads only those blocks. Output is capped at about 8000 characters; when more blocks follow, the result says which start to use next.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "start": {"type": "integer", "description": "1-based index of the first block to read. Default 1."},
                "count": {"type": "integer", "description": "How many blocks to read, 1-200. Default 60."},
                "detail": {"type": "string", "enum": ["text", "format"], "description": "Default text."},
                "find": {
                  "type": "object",
                  "properties": {
                    "text": {"type": "string"},
                    "match_case": {"type": "boolean"},
                    "whole_word": {"type": "boolean"}
                  },
                  "required": ["text"]
                },
                "ids": {"type": "array", "items": {"type": "string"}, "description": "Read only these blocks."}
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

            var format = ParseDetail(input);
            var find = ParseFind(input);
            var ids = input.Has("ids") ? input.GetStringList("ids") : null;
            if (ids != null && ids.Count == 0)
                throw new ToolInputException("'ids' 값에 id를 하나 이상 넣어 주세요.");

            if (ids != null && ids.Count > MaxIds)
                throw new ToolInputException($"'ids'는 한 번에 {MaxIds}개까지만 넣을 수 있어요.");

            var scan = WordScan.Read(docu.Document);
            var blocks = scan.Blocks;
            if (blocks.Count == 0)
                return ToolResult.Ok("The document has no paragraphs yet. Add some with insert_paragraphs and after=\"start\".", "빈 문서");

            var describer = format ? new WordFormatDescriber(scan) : null;
            if (ids != null)
                return ReadIds(scan, ids, find, describer);

            if (find != null)
                return Find(scan, start, count, find, describer);

            if (start > blocks.Count)
                throw new ToolInputException($"이 문서에는 블록이 {blocks.Count}개 있어요. start는 1~{blocks.Count} 사이로 넣어 주세요.");

            var body = new StringBuilder();
            var shown = new List<WordBlock>();
            for (var i = start - 1; i < blocks.Count && shown.Count < count; i++)
            {
                var entry = Format(blocks[i], shown.Count == 0, describer);
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

            return ToolResult.Ok(text.ToString().TrimEnd(), Detail(shown));
        }

        private static ToolResult ReadIds(WordScan scan, IReadOnlyList<string> ids, FindOptions find, WordFormatDescriber describer)
        {
            var wanted = new List<WordBlock>();
            foreach (var id in ids)
            {
                var block = scan.Find(id) ?? throw NotFound(id);
                if (!wanted.Contains(block))
                    wanted.Add(block);
            }

            wanted.Sort((a, b) => a.Index.CompareTo(b.Index));
            if (find != null)
            {
                var hits = Matches(wanted, find).ToList();
                return Report(scan, hits, find, describer, wanted.Count, 1, int.MaxValue, true);
            }

            var body = new StringBuilder();
            var shown = new List<WordBlock>();
            foreach (var block in wanted)
            {
                var entry = Format(block, shown.Count == 0, describer);
                if (shown.Count > 0 && body.Length + entry.Length > Budget)
                    break;

                body.Append(entry);
                shown.Add(block);
            }

            var text = new StringBuilder();
            text.Append(CultureInfo.InvariantCulture, $"Blocks {string.Join(", ", shown.Select(b => b.Index))} of {scan.Blocks.Count}, about {scan.PageCount} page(s); page numbers are estimates.\n");
            text.Append(body);
            if (shown.Count < wanted.Count)
                text.Append(CultureInfo.InvariantCulture, $"Output cap reached. Call read_paragraphs with ids=[{string.Join(", ", wanted.Skip(shown.Count).Select(b => "\"" + b.Id + "\""))}] to continue.");

            return ToolResult.Ok(text.ToString().TrimEnd(), Detail(shown));
        }

        private static ToolResult Find(WordScan scan, int start, int count, FindOptions find, WordFormatDescriber describer)
        {
            if (start > scan.Blocks.Count)
                throw new ToolInputException($"이 문서에는 블록이 {scan.Blocks.Count}개 있어요. start는 1~{scan.Blocks.Count} 사이로 넣어 주세요.");

            var candidates = scan.Blocks.Skip(start - 1).ToList();
            var hits = Matches(candidates, find).ToList();
            return Report(scan, hits, find, describer, candidates.Count, start, count, false);
        }

        private static ToolResult Report(WordScan scan, List<Hit> hits, FindOptions find, WordFormatDescriber describer, int searched, int start, int count, bool byIds)
        {
            var total = hits.Sum(h => h.Offsets.Count);
            var blockCount = hits.Select(h => h.Block).Distinct().Count();
            var quoted = "\"" + Shorten(find.Text, 60) + "\"";
            var options = (find.MatchCase ? "case-sensitive" : "case-insensitive") + (find.WholeWord ? ", whole word" : string.Empty);
            if (hits.Count == 0)
                return ToolResult.Ok($"No match for {quoted} ({options}) in {searched} block(s).", "찾은 글 0곳");

            var body = new StringBuilder();
            var shown = 0;
            WordBlock cut = null;
            foreach (var hit in hits)
            {
                if (shown >= count)
                {
                    cut = hit.Block;
                    break;
                }

                var entry = FormatHit(hit, shown == 0, describer);
                if (shown > 0 && body.Length + entry.Length > Budget)
                {
                    cut = hit.Block;
                    break;
                }

                body.Append(entry);
                shown++;
            }

            var text = new StringBuilder();
            text.Append(CultureInfo.InvariantCulture, $"Found {total} match(es) for {quoted} ({options}) in {blockCount} block(s) of {scan.Blocks.Count}");
            text.Append(start > 1 && !byIds ? CultureInfo.InvariantCulture : CultureInfo.InvariantCulture, $"{(start > 1 && !byIds ? $", searching from block {start}" : string.Empty)}; offsets are 0-based character positions.\n");
            text.Append(body);
            if (cut != null)
                text.Append(CultureInfo.InvariantCulture, $"More matches follow. Call read_paragraphs with the same find and start={cut.Index} to continue.");

            return ToolResult.Ok(text.ToString().TrimEnd(), $"찾은 글 {total}곳");
        }

        private static IEnumerable<Hit> Matches(IEnumerable<WordBlock> blocks, FindOptions find)
        {
            foreach (var block in blocks)
            {
                if (!block.IsTable)
                {
                    var offsets = WordTextSearch.Find(WordTextMap.Build(block.Paragraph).Text, find.Text, find.MatchCase, find.WholeWord);
                    if (offsets.Count > 0)
                        yield return new Hit { Block = block, Offsets = offsets };

                    continue;
                }

                var grid = WordTables.Grid(block.Table);
                for (var r = 0; r < grid.Count; r++)
                {
                    for (var c = 0; c < grid[r].Count; c++)
                    {
                        if (!grid[r][c].IsOrigin || grid[r][c].Owner == null)
                            continue;

                        var cell = grid[r][c].Owner;
                        var joined = string.Join("\n", cell.Elements<Paragraph>().Select(p => WordTextMap.Build(p).Text));
                        var offsets = WordTextSearch.Find(joined, find.Text, find.MatchCase, find.WholeWord);
                        if (offsets.Count > 0)
                            yield return new Hit { Block = block, Cell = cell, Row = r + 1, Column = c + 1, Text = joined, Offsets = offsets };
                    }
                }
            }
        }

        private static string FormatHit(Hit hit, bool focused, WordFormatDescriber describer)
        {
            var block = hit.Block;
            var line = new StringBuilder();
            line.Append(CultureInfo.InvariantCulture, $"{block.Index}. [{block.Id ?? "no id"}] ");
            string shown;
            if (hit.Cell != null)
            {
                line.Append(CultureInfo.InvariantCulture, $"table, p{block.Page}, row {hit.Row}, column {hit.Column}: ");
                shown = describer == null
                    ? Escape(Shorten(hit.Text, ParagraphLimit))
                    : string.Join("\\n", hit.Cell.Elements<Paragraph>().Select(p => describer.Markup(p, ParagraphLimit, true)));
            }
            else
            {
                line.Append(KindName(block.Kind));
                if ((block.Kind == WordKind.Bullet || block.Kind == WordKind.Number) && block.ListLevel > 0)
                    line.Append(CultureInfo.InvariantCulture, $" level {block.ListLevel + 1}");

                line.Append(CultureInfo.InvariantCulture, $", p{block.Page}");
                AppendLook(line, describer, block);
                line.Append(": ");
                shown = describer == null ? Escape(Shorten(block.Text, ParagraphLimit)) : describer.Markup(block.Paragraph, ParagraphLimit);
            }

            line.Append(shown);
            line.Append(CultureInfo.InvariantCulture, $" (matches at {string.Join(", ", hit.Offsets)})\n");
            return line.ToString();
        }

        private static string Detail(List<WordBlock> shown)
        {
            var tables = shown.Count(b => b.IsTable);
            var paragraphs = shown.Count - tables;
            return tables > 0 ? $"문단 {paragraphs}개, 표 {tables}개" : $"문단 {paragraphs}개";
        }

        private static bool ParseDetail(ToolInput input)
        {
            var value = input.GetOptionalString("detail");
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "text", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(value.Trim(), "format", StringComparison.OrdinalIgnoreCase))
                return true;

            throw new ToolInputException("'detail' 값은 text 또는 format 중 하나여야 해요.");
        }

        private static FindOptions ParseFind(ToolInput input)
        {
            if (!input.Has("find"))
                return null;

            var element = input.Get("find");
            if (element.ValueKind == JsonValueKind.String)
                element = JsonDocument.Parse(JsonSerializer.Serialize(new { text = element.GetString() })).RootElement.Clone();

            if (element.ValueKind != JsonValueKind.Object)
                throw new ToolInputException("'find' 값은 {\"text\": \"찾을 글\"} 형태여야 해요.");

            var nested = new ToolInput(element);
            var text = nested.Has("text") ? ToolInput.AsText(element.GetProperty("text")) : null;
            if (string.IsNullOrEmpty(text))
                throw new ToolInputException("'find'의 text에 찾을 글을 넣어 주세요.");

            return new FindOptions
            {
                Text = text,
                MatchCase = nested.GetOptionalBool("match_case") ?? false,
                WholeWord = nested.GetOptionalBool("whole_word") ?? false,
            };
        }

        private static string Format(WordBlock block, bool focused, WordFormatDescriber describer)
        {
            var line = new StringBuilder();
            line.Append(CultureInfo.InvariantCulture, $"{block.Index}. [{block.Id ?? "no id"}] ");
            if (block.IsTable)
            {
                AppendTable(line, block, focused, describer);
                return line.ToString();
            }

            line.Append(KindName(block.Kind));
            if ((block.Kind == WordKind.Bullet || block.Kind == WordKind.Number) && block.ListLevel > 0)
                line.Append(CultureInfo.InvariantCulture, $" level {block.ListLevel + 1}");

            if (block.IsHeading && block.IsNumbered)
                line.Append(" auto-numbered");

            line.Append(CultureInfo.InvariantCulture, $", p{block.Page}");
            AppendLook(line, describer, block);
            line.Append(": ");
            if (describer == null)
                line.Append(block.IsEmpty && block.Text.Length == 0 ? "(empty)" : Escape(Shorten(block.Text, ParagraphLimit)));
            else
                line.Append(block.IsEmpty && block.Text.Length == 0 ? "(empty)" : describer.Markup(block.Paragraph, ParagraphLimit));

            line.Append('\n');
            return line.ToString();
        }

        private static void AppendLook(StringBuilder line, WordFormatDescriber describer, WordBlock block)
        {
            if (describer == null)
                return;

            var look = describer.DescribeParagraph(block.Paragraph);
            if (look.Length > 0)
                line.Append(" (").Append(look).Append(')');
        }

        private static void AppendTable(StringBuilder line, WordBlock block, bool focused, WordFormatDescriber describer)
        {
            var grid = WordTables.Grid(block.Table);
            var rows = describer == null ? WordTables.Texts(grid) : MarkedRows(grid, describer, focused ? CellLimit * 4 : CellLimit);
            var columns = WordTables.ColumnCount(grid);
            line.Append(CultureInfo.InvariantCulture, $"table {rows.Count}x{columns}, p{block.Page}");
            var style = describer == null ? null : block.Table.TableProperties?.TableStyle?.Val?.Value;
            if (style != null)
                line.Append(CultureInfo.InvariantCulture, $", style \"{describer.StyleName(style)}\"");

            line.Append(":\n");

            var limit = focused ? FocusedRows : ListedRows;
            var cellLimit = focused ? CellLimit * 4 : CellLimit;
            var written = 0;
            foreach (var row in rows)
            {
                if (written >= limit || (focused && line.Length > Budget))
                    break;

                line.Append("   ");
                line.Append(string.Join(" | ", row.Select(cell => describer == null ? Escape(Shorten(cell, cellLimit)) : cell)));
                line.Append('\n');
                written++;
            }

            if (written < rows.Count && focused)
                line.Append(CultureInfo.InvariantCulture, $"   … {rows.Count - written} more rows not shown.\n");
            else if (written < rows.Count)
                line.Append(CultureInfo.InvariantCulture, $"   … {rows.Count - written} more rows not shown. Call read_paragraphs with start={block.Index} and count=1 to see more.\n");
        }

        private static List<List<string>> MarkedRows(List<List<WordCell>> grid, WordFormatDescriber describer, int limit) =>
            grid.Select(row => row.Select(c => c.IsOrigin && c.Owner != null
                ? string.Join("\\n", c.Owner.Elements<Paragraph>().Select(p => describer.Markup(p, limit, true)))
                : string.Empty).ToList()).ToList();

        private static string Shorten(string text, int limit) =>
            text.Length <= limit ? text : text.Substring(0, limit) + $"… (+{text.Length - limit} chars)";

        private static string Escape(string text) => text.Replace("\n", "\\n").Replace("\t", "\\t");

        private sealed class FindOptions
        {
            public string Text { get; set; }

            public bool MatchCase { get; set; }

            public bool WholeWord { get; set; }
        }

        private sealed class Hit
        {
            public WordBlock Block { get; set; }

            public TableCell Cell { get; set; }

            public int Row { get; set; }

            public int Column { get; set; }

            public string Text { get; set; }

            public IReadOnlyList<int> Offsets { get; set; }
        }
    }
}
