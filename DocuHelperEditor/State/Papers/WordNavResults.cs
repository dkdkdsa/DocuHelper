using System;
using System.Collections.Generic;
using DocuHelper.Tools.Word;

namespace DocuHelperEditor.State.Papers
{
    public sealed record WordNavHit(string BlockId, int Start, int End, int Page, string Before, string Match, string After);

    public static class WordNavResults
    {
        private const int ContextLength = 18;

        public static IReadOnlyList<WordNavHit> Find(WordPaperModel model, string query)
        {
            var hits = new List<WordNavHit>();
            var needle = query?.Trim() ?? string.Empty;
            if (model == null || needle.Length == 0)
                return hits;

            foreach (var block in model.Blocks)
            {
                if (block.IsTable)
                    continue;

                var text = block.Text ?? string.Empty;
                foreach (var start in WordTextSearch.Find(text, needle, false, false))
                {
                    var end = start + needle.Length;
                    var from = Math.Max(0, start - ContextLength);
                    var before = (from > 0 ? "…" : string.Empty) + text.Substring(from, start - from);
                    var to = Math.Min(text.Length, end + ContextLength);
                    var after = text.Substring(end, to - end) + (to < text.Length ? "…" : string.Empty);
                    hits.Add(new WordNavHit(block.Id, start, end, block.Page, before, text.Substring(start, needle.Length), after));
                }
            }

            return hits;
        }
    }
}
