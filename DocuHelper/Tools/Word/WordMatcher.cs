using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DocuHelper.Tools.Word
{
    public sealed class WordMatcher
    {
        private readonly string _query;
        private readonly bool _matchCase;
        private readonly bool _wholeWord;
        private readonly Regex _regex;

        private WordMatcher(string query, bool matchCase, bool wholeWord, Regex regex)
        {
            _query = query;
            _matchCase = matchCase;
            _wholeWord = wholeWord;
            _regex = regex;
        }

        public static WordMatcher Create(string query, WordFindOptions options)
        {
            if (string.IsNullOrEmpty(query))
                throw new ToolInputException("찾을 글을 넣어 주세요.");

            options ??= new WordFindOptions();
            Regex regex = null;
            if (options.Regex)
            {
                var flags = RegexOptions.CultureInvariant | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
                try
                {
                    regex = new Regex(query, flags, TimeSpan.FromSeconds(2));
                }
                catch (ArgumentException)
                {
                    throw new ToolInputException("정규식이 올바르지 않아요. 식을 다시 확인해 주세요.");
                }
            }

            return new WordMatcher(query, options.MatchCase, options.WholeWord, regex);
        }

        public IReadOnlyList<(int Start, int End, Match Match)> Matches(string text)
        {
            var found = new List<(int, int, Match)>();
            if (string.IsNullOrEmpty(text))
                return found;

            if (_regex == null)
            {
                foreach (var index in WordTextSearch.Find(text, _query, _matchCase, _wholeWord))
                    found.Add((index, index + _query.Length, null));

                return found;
            }

            try
            {
                for (var match = _regex.Match(text); match.Success; match = match.NextMatch())
                {
                    if (match.Length > 0 && (!_wholeWord || IsWhole(text, match.Index, match.Length)))
                        found.Add((match.Index, match.Index + match.Length, match));
                }
            }
            catch (RegexMatchTimeoutException)
            {
                throw new ToolInputException("정규식이 너무 오래 걸려서 멈췄어요. 더 단순한 식으로 다시 시도해 주세요.");
            }

            return found;
        }

        public string Replacement(Match match, string replacement)
        {
            if (match == null)
                return replacement;

            try
            {
                return match.Result(replacement);
            }
            catch (ArgumentException)
            {
                throw new ToolInputException("바꿀 글의 $ 표기가 올바르지 않아요. 글자 그대로의 $는 $$로 써 주세요.");
            }
        }

        private static bool IsWhole(string text, int start, int length)
        {
            var end = start + length;
            var before = start == 0 || !IsWordChar(text[start - 1]) || !IsWordChar(text[start]);
            var after = end >= text.Length || !IsWordChar(text[end]) || !IsWordChar(text[end - 1]);
            return before && after;
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
