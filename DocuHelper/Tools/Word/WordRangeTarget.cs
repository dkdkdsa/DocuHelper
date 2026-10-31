using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordRangeTarget
    {
        public const int MaxTargets = 1000;

        public static IReadOnlyList<WordRange> Resolve(WordScan scan, ToolInput input, string name = "targets") =>
            Resolve(scan, input.Get(name), name);

        public static IReadOnlyList<WordRange> Resolve(WordScan scan, JsonElement element, string name)
        {
            var ranges = new List<WordRange>();
            switch (element.ValueKind)
            {
                case JsonValueKind.Array:
                {
                    var index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        index++;
                        ranges.AddRange(ResolveOne(scan, item, $"{name}[{index}]"));
                        Cap(ranges, name);
                    }

                    break;
                }

                case JsonValueKind.Object:
                case JsonValueKind.String:
                case JsonValueKind.Number:
                    ranges.AddRange(ResolveOne(scan, element, name));
                    break;
                default:
                    throw new ToolInputException($"'{name}' 값은 대상 목록이어야 해요. 예: [{{\"id\": \"...\", \"start\": 0, \"end\": 5}}]");
            }

            if (ranges.Count == 0)
                throw new ToolInputException($"'{name}'에 대상이 하나도 없어요.");

            return ranges;
        }

        public static IReadOnlyList<WordRange> ResolveOne(WordScan scan, JsonElement item, string label)
        {
            var target = Parse(item, label);
            var block = Find(scan, target.Id);
            if (block.IsTable)
                return Table(block, target, label);

            if (target.Row != null || target.Column != null)
                throw new ToolInputException($"'{label}'의 ‘{block.Id}’는 문단이라 row, column을 쓸 수 없어요. row, column은 표 id에만 써요.");

            return Within(block, null, new List<Paragraph> { block.Paragraph }, target, label);
        }

        public static WordPoint ResolveCaret(WordScan scan, ToolInput input, string name) =>
            ResolveCaret(scan, input.Get(name), name);

        public static WordPoint ResolveCaret(WordScan scan, JsonElement element, string label)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var word = (element.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                if (word == "start" || word == "end")
                    return Edge(scan, word == "start", label);
            }

            var target = Parse(element, label);
            var block = Find(scan, target.Id);
            if (block.IsTable)
            {
                if (target.Row == null || target.Column == null)
                    throw new ToolInputException($"'{label}'의 ‘{block.Id}’는 표예요. row와 column으로 칸을 정해 주세요.");

                var cell = CellOf(block, target, label);
                var paragraphs = cell.Elements<Paragraph>().ToList();
                var joined = Joined(paragraphs);
                var offset = target.Offset ?? target.Start ?? (target.Match != null ? Pieces(joined, target, label)[0].Start : joined.Length);
                Bounds(offset, offset, joined.Length, block.Id, label);
                return PointIn(block, cell, paragraphs, offset);
            }

            if (target.Row != null || target.Column != null)
                throw new ToolInputException($"'{label}'의 ‘{block.Id}’는 문단이라 row, column을 쓸 수 없어요. row, column은 표 id에만 써요.");

            var map = WordTextMap.Build(block.Paragraph);
            var caret = target.Offset ?? target.Start ?? (target.Match != null ? Pieces(map.Text, target, label)[0].Start : map.Length);
            Bounds(caret, caret, map.Length, block.Id, label);
            return new WordPoint(block, null, block.Paragraph, map.SnapUp(caret));
        }

        private sealed class Target
        {
            public string Id { get; set; }

            public int? Row { get; set; }

            public int? Column { get; set; }

            public int? Start { get; set; }

            public int? End { get; set; }

            public int? Offset { get; set; }

            public string Match { get; set; }

            public int? Occurrence { get; set; }
        }

        private static Target Parse(JsonElement item, string label)
        {
            var target = new Target();
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                case JsonValueKind.Number:
                    target.Id = ToolInput.AsText(item);
                    break;
                case JsonValueKind.Object:
                    var input = new ToolInput(item);
                    target.Id = input.GetOptionalString("id");
                    target.Row = Whole(input, "row", label);
                    target.Column = Whole(input, "column", label);
                    target.Start = Whole(input, "start", label);
                    target.End = Whole(input, "end", label);
                    target.Offset = Whole(input, "offset", label);
                    target.Match = input.GetOptionalString("match");
                    target.Occurrence = Whole(input, "occurrence", label);
                    break;
                default:
                    throw new ToolInputException($"'{label}'은 문단 id이거나 id가 들어 있는 객체여야 해요.");
            }

            if (string.IsNullOrWhiteSpace(target.Id))
                throw new ToolInputException($"'{label}'에 id가 필요해요. read_paragraphs로 id를 확인해 주세요.");

            var at = target.Id.IndexOf('@');
            if (at >= 0)
            {
                if (!WordAnchors.TryParse(WordIds.ParagraphPrefix + WordIds.Normalize(target.Id.Substring(0, at)) + target.Id.Substring(at), out var anchor) || !anchor.HasRange)
                    throw new ToolInputException($"'{label}'의 id ‘{target.Id}’ 형식이 올바르지 않아요.");

                target.Id = anchor.Id;
                target.Start ??= anchor.Start;
                target.End ??= anchor.End;
            }

            if (target.Match != null && target.Match.Length == 0)
                throw new ToolInputException($"'{label}'의 match가 비어 있어요.");

            if (target.Occurrence != null && target.Match == null)
                throw new ToolInputException($"'{label}'의 occurrence는 match와 함께 써요.");

            if (target.Match != null && target.Offset != null)
                throw new ToolInputException($"'{label}'에는 match와 offset을 함께 쓸 수 없어요.");

            if ((target.Row == null) != (target.Column == null))
                throw new ToolInputException($"'{label}'의 row와 column은 함께 넣어 주세요.");

            return target;
        }

        private static int? Whole(ToolInput input, string name, string label)
        {
            try
            {
                return input.GetOptionalInt(name);
            }
            catch (ToolInputException)
            {
                throw new ToolInputException($"'{label}'의 {name} 값은 정수여야 해요.");
            }
        }

        private static WordBlock Find(WordScan scan, string id)
        {
            var key = WordIds.Normalize(id);
            return scan.Find(key) ?? throw new ToolInputException($"‘{key ?? id}’ 문단을 찾지 못했어요. read_paragraphs로 id를 다시 확인해 주세요.");
        }

        private static IReadOnlyList<WordRange> Table(WordBlock block, Target target, string label)
        {
            if (target.Row == null)
            {
                if (target.Start != null || target.End != null || target.Match != null || target.Offset != null)
                    throw new ToolInputException($"'{label}'의 ‘{block.Id}’는 표예요. 글자 구간을 쓰려면 row와 column으로 칸을 정해 주세요.");

                var whole = new List<WordRange>();
                foreach (var cell in WordTables.Grid(block.Table).SelectMany(r => r).Where(c => c.IsOrigin).Select(c => c.Cell).Distinct())
                {
                    foreach (var paragraph in cell.Elements<Paragraph>())
                    {
                        whole.Add(new WordRange(block, cell, paragraph, 0, WordTextMap.Build(paragraph).Length));
                        Cap(whole, label);
                    }
                }

                return whole;
            }

            var owner = CellOf(block, target, label);
            return Within(block, owner, owner.Elements<Paragraph>().ToList(), target, label);
        }

        private static TableCell CellOf(WordBlock block, Target target, string label)
        {
            var grid = WordTables.Grid(block.Table);
            var row = target.Row.Value;
            var column = target.Column.Value;
            if (row < 1 || row > grid.Count)
                throw new ToolInputException($"'{label}': 이 표는 {grid.Count}줄이에요. row는 1~{grid.Count} 사이로 넣어 주세요.");

            var columns = grid[row - 1].Count;
            if (column < 1 || column > columns)
                throw new ToolInputException($"'{label}': {row}번째 줄은 {columns}칸이에요. column은 1~{columns} 사이로 넣어 주세요.");

            var cell = WordTables.CellAt(grid, row - 1, column - 1);
            if (cell == null || !cell.Elements<Paragraph>().Any())
                throw new ToolInputException($"'{label}': {row}행 {column}열은 비어 있는 칸이라 글을 고를 수 없어요.");

            return cell;
        }

        private static string Joined(List<Paragraph> paragraphs) => string.Join("\n", paragraphs.Select(WordText.Of));

        private static IReadOnlyList<WordRange> Within(WordBlock block, TableCell cell, List<Paragraph> paragraphs, Target target, string label)
        {
            var joined = Joined(paragraphs);
            var ranges = new List<WordRange>();
            foreach (var piece in Pieces(joined, target, label))
            {
                Bounds(piece.Start, piece.End, joined.Length, block.Id, label);
                var begin = 0;
                for (var i = 0; i < paragraphs.Count; i++)
                {
                    var map = WordTextMap.Build(paragraphs[i]);
                    var length = map.Length;
                    var from = Math.Max(piece.Start, begin);
                    var to = Math.Min(piece.End, begin + length);
                    var collapsed = piece.Start == piece.End;
                    var covers = from < to
                        || (collapsed && piece.Start >= begin && piece.Start <= begin + length)
                        || (length == 0 && piece.Start <= begin && begin <= piece.End);
                    if (covers)
                    {
                        var start = map.SnapDown(Math.Max(0, from - begin));
                        var end = map.SnapUp(Math.Max(start, to - begin));
                        ranges.Add(new WordRange(block, cell, paragraphs[i], start, Math.Min(end, length)));
                        if (collapsed)
                            break;
                    }

                    begin += length + 1;
                }
            }

            return ranges;
        }

        private static List<(int Start, int End)> Pieces(string text, Target target, string label)
        {
            var pieces = new List<(int Start, int End)>();
            if (target.Match == null)
            {
                var start = target.Start ?? 0;
                var end = target.End ?? text.Length;
                pieces.Add((start, end));
                return pieces;
            }

            var from = target.Start ?? 0;
            var to = target.End ?? text.Length;
            if (from < 0 || to < from || to > text.Length)
                throw new ToolInputException($"'{label}': 글자 위치 {from}~{to}가 글 길이 {text.Length}를 벗어났어요.");

            var found = new List<int>();
            for (var at = from; at + target.Match.Length <= to;)
            {
                var index = text.IndexOf(target.Match, at, to - at, StringComparison.Ordinal);
                if (index < 0)
                    break;

                found.Add(index);
                at = index + target.Match.Length;
            }

            if (found.Count == 0)
                throw new ToolInputException($"'{label}': ‘{Shorten(target.Match)}’ 글을 찾지 못했어요. 문단 글을 read_paragraphs로 다시 확인해 주세요.");

            if (target.Occurrence == null)
            {
                if (found.Count > 1)
                    throw new ToolInputException($"'{label}': ‘{Shorten(target.Match)}’ 글이 {found.Count}곳에 있어요. occurrence에 1~{found.Count} 중 몇 번째인지 넣거나, 0을 넣어 모두 골라 주세요.");

                pieces.Add((found[0], found[0] + target.Match.Length));
                return pieces;
            }

            var occurrence = target.Occurrence.Value;
            if (occurrence == 0)
            {
                pieces.AddRange(found.Select(f => (f, f + target.Match.Length)));
                return pieces;
            }

            if (occurrence < 1 || occurrence > found.Count)
                throw new ToolInputException($"'{label}': ‘{Shorten(target.Match)}’ 글은 {found.Count}곳뿐이라 occurrence는 1~{found.Count} 사이(0은 모두)여야 해요.");

            pieces.Add((found[occurrence - 1], found[occurrence - 1] + target.Match.Length));
            return pieces;
        }

        private static WordPoint PointIn(WordBlock block, TableCell cell, List<Paragraph> paragraphs, int offset)
        {
            var begin = 0;
            for (var i = 0; i < paragraphs.Count; i++)
            {
                var map = WordTextMap.Build(paragraphs[i]);
                if (offset <= begin + map.Length || i == paragraphs.Count - 1)
                    return new WordPoint(block, cell, paragraphs[i], map.SnapUp(Math.Min(map.Length, Math.Max(0, offset - begin))));

                begin += map.Length + 1;
            }

            return new WordPoint(block, cell, paragraphs[^1], 0);
        }

        private static WordPoint Edge(WordScan scan, bool first, string label)
        {
            var block = first ? scan.Blocks.FirstOrDefault() : scan.Blocks.LastOrDefault();
            if (block == null || block.IsTable)
                throw new ToolInputException($"'{label}': 문서 {(first ? "처음" : "끝")}이 표라서 글자 위치를 정할 수 없어요. 문단 id로 정해 주세요.");

            var map = WordTextMap.Build(block.Paragraph);
            return new WordPoint(block, null, block.Paragraph, first ? 0 : map.Length);
        }

        private static void Bounds(int start, int end, int length, string id, string label)
        {
            if (start < 0 || end < start || end > length)
                throw new ToolInputException($"'{label}': 글자 위치 {start}~{end}가 ‘{id}’의 글 길이 {length}를 벗어났어요. 0~{length} 사이로 넣어 주세요.");
        }

        private static void Cap(List<WordRange> ranges, string label)
        {
            if (ranges.Count > MaxTargets)
                throw new ToolInputException($"'{label}'의 대상이 너무 많아요. 한 번에 {MaxTargets}곳까지만 고를 수 있어요.");
        }

        private static string Shorten(string text) => text.Length <= 30 ? text : text.Substring(0, 30) + "…";
    }
}
