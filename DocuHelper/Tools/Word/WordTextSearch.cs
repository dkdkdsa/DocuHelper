using System;
using System.Collections.Generic;

namespace DocuHelper.Tools.Word
{
    public static class WordTextSearch
    {
        public static IReadOnlyList<int> Find(string text, string query, bool matchCase, bool wholeWord)
        {
            var found = new List<int>();
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
                return found;

            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            for (var at = 0; at + query.Length <= text.Length;)
            {
                var index = text.IndexOf(query, at, comparison);
                if (index < 0)
                    break;

                if (!wholeWord || IsWhole(text, index, query.Length))
                {
                    found.Add(index);
                    at = index + query.Length;
                }
                else
                {
                    at = index + 1;
                }
            }

            return found;
        }

        private static bool IsWhole(string text, int start, int length)
        {
            var before = start == 0 || !IsWordChar(text[start - 1]) || !IsWordChar(text[start]);
            var end = start + length;
            var after = end >= text.Length || !IsWordChar(text[end]) || !IsWordChar(text[end - 1]);
            return before && after;
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
