using System;
using System.Collections.Generic;
using DocuHelper.Tools.Word;

namespace DocuHelperEditor.State.Papers
{
    public sealed record WordNavHit(string BlockId, int Start, int End, int Page, string Before, string Match, string After, int Row = -1, int Column = -1);

    public static class WordNavResults
    {
        private const int ContextLength = 18;

        public static IReadOnlyList<WordNavHit> Find(WordPaperModel model, string query, bool matchCase = false, bool wholeWord = false)
        {
            var hits = new List<WordNavHit>();
            var needle = query?.Trim() ?? string.Empty;
            if (model == null || needle.Length == 0)
                return hits;

            foreach (var block in model.Blocks)
            {
                if (!block.IsTable)
                {
                    Collect(hits, block, block.Text ?? string.Empty, needle, matchCase, wholeWord, -1, -1);
                    continue;
                }

                var rows = block.Table?.Rows ?? new List<List<string>>();
                for (var row = 0; row < rows.Count; row++)
                {
                    var cells = rows[row] ?? new List<string>();
                    for (var column = 0; column < cells.Count; column++)
                        Collect(hits, block, cells[column] ?? string.Empty, needle, matchCase, wholeWord, row, column);
                }
            }

            return hits;
        }

        public static WordSelection SelectionOf(WordNavHit hit) =>
            hit.Row < 0
                ? new WordSelection(WordSelectionKind.Text, null, new[] { new WordSelectionRange(hit.BlockId, hit.Start, hit.End) })
                : new WordSelection(new WordCellRange(hit.BlockId, hit.Row + 1, hit.Column + 1, hit.Row + 1, hit.Column + 1));

        private static void Collect(List<WordNavHit> hits, WordBlockModel block, string text, string needle, bool matchCase, bool wholeWord, int row, int column)
        {
            foreach (var start in WordTextSearch.Find(text, needle, matchCase, wholeWord))
            {
                var end = start + needle.Length;
                var from = Math.Max(0, start - ContextLength);
                var before = (from > 0 ? "…" : string.Empty) + text.Substring(from, start - from);
                var to = Math.Min(text.Length, end + ContextLength);
                var after = text.Substring(end, to - end) + (to < text.Length ? "…" : string.Empty);
                hits.Add(new WordNavHit(block.Id, start, end, block.Page, before, text.Substring(start, needle.Length), after, row, column));
            }
        }
    }
}
