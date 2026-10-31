using System.Collections.Generic;

namespace DocuHelper.Tools.Word
{
    public sealed class WordRevisionOutcome
    {
        public int Count { get; set; }

        public int Skipped { get; set; }

        public List<string> ParagraphIds { get; } = new();
    }
}
