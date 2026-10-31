using System.Collections.Generic;

namespace DocuHelper.Tools.Word
{
    public sealed class WordFindOptions
    {
        public bool MatchCase { get; set; }

        public bool WholeWord { get; set; }

        public bool Regex { get; set; }

        public WordFindParts Parts { get; set; } = WordFindParts.Body | WordFindParts.Tables;

        public IReadOnlyList<string> Scope { get; set; }

        public int? Limit { get; set; }
    }
}
