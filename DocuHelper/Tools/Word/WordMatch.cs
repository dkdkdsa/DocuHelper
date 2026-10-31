namespace DocuHelper.Tools.Word
{
    public sealed class WordMatch
    {
        public WordMatch(WordFindSite site, int start, int end, string value, string before, string after)
        {
            Site = site;
            Start = start;
            End = end;
            Value = value;
            Before = before;
            After = after;
        }

        public WordFindSite Site { get; }

        public int Start { get; }

        public int End { get; }

        public string Value { get; }

        public string Before { get; }

        public string After { get; }

        public int Page => Site.Block?.Page ?? 0;

        public string Anchor => Site.Anchor;
    }
}
