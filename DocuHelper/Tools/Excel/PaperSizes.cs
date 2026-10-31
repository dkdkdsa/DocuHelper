using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    internal static class PaperSizes
    {
        public const string Default = "a4";

        public sealed record Paper(string Key, uint Code, double WidthCm, double HeightCm);

        private static readonly Paper[] s_papers =
        {
            new("a3", 8, 29.7, 42.0),
            new("a4", 9, 21.0, 29.7),
            new("a5", 11, 14.8, 21.0),
            new("b4_jis", 12, 25.7, 36.4),
            new("b5_jis", 13, 18.2, 25.7),
            new("letter", 1, 21.59, 27.94),
            new("legal", 5, 21.59, 35.56),
            new("tabloid", 3, 27.94, 43.18),
        };

        public static IEnumerable<string> Keys => s_papers.Select(p => p.Key);

        public static Paper ByKey(string key) => s_papers.FirstOrDefault(p => p.Key == key);

        public static Paper ByCode(uint code) => s_papers.FirstOrDefault(p => p.Code == code);

        public static Paper Fallback => ByKey(Default);
    }
}
