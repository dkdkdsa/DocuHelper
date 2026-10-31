using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpStyleNames
    {
        private const int HangulOutlineLevels = 7;
        private const string FooterKey = "footer";
        private const string HeaderHangul = "머리말";

        private static readonly (string Hangul, string Word)[] s_aliases = BuildAliases();

        private static readonly Dictionary<string, (string Hangul, string Word)> s_byHangul = Index(a => a.Hangul);

        private static readonly Dictionary<string, (string Hangul, string Word)> s_byWord = Index(a => a.Word);

        private static readonly string[] s_outlinePrefixes = { "heading", "제목", "개요" };

        public static IReadOnlyList<(string Hangul, string Word)> Aliases => s_aliases;

        public static string ToHangul(string name)
        {
            var key = Key(name);
            if (s_byHangul.TryGetValue(key, out var alias) || s_byWord.TryGetValue(key, out alias))
                return alias.Hangul;

            if (key == FooterKey)
                return HeaderHangul;

            return OutlineLevel(key) is int level && level <= HangulOutlineLevels ? "개요 " + level : null;
        }

        public static string ToWord(string name)
        {
            var key = Key(name);
            if (s_byHangul.TryGetValue(key, out var alias) || s_byWord.TryGetValue(key, out alias))
                return alias.Word;

            return OutlineLevel(key) is int level && level <= HangulOutlineLevels ? "Heading " + level : null;
        }

        public static int? OutlineLevel(string name)
        {
            var key = Key(name);
            foreach (var prefix in s_outlinePrefixes)
            {
                if (key.Length == prefix.Length + 1 && key.StartsWith(prefix, StringComparison.Ordinal) && key[^1] >= '1' && key[^1] <= '9')
                    return key[^1] - '0';
            }

            return null;
        }

        private static (string Hangul, string Word)[] BuildAliases()
        {
            var aliases = new List<(string Hangul, string Word)> { ("바탕글", "Normal"), ("본문", "Body Text") };
            for (var level = 1; level <= HangulOutlineLevels; level++)
                aliases.Add(("개요 " + level, "Heading " + level));

            aliases.Add(("캡션", "Caption"));
            aliases.Add(("쪽 번호", "Page Number"));
            aliases.Add((HeaderHangul, "Header"));
            aliases.Add(("각주", "Footnote Text"));
            aliases.Add(("미주", "Endnote Text"));
            return aliases.ToArray();
        }

        private static Dictionary<string, (string Hangul, string Word)> Index(Func<(string Hangul, string Word), string> name) =>
            s_aliases.ToDictionary(a => Key(name(a)), StringComparer.Ordinal);

        private static string Key(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            return new string(name.Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-').Select(char.ToLowerInvariant).ToArray());
        }
    }
}
