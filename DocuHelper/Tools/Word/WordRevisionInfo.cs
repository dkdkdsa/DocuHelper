using System;

namespace DocuHelper.Tools.Word
{
    public sealed class WordRevisionInfo
    {
        public string Id { get; init; }

        public string Kind { get; init; }

        public string Detail { get; init; }

        public string Author { get; init; }

        public DateTime? Date { get; init; }

        public string Text { get; init; } = string.Empty;

        public string Where { get; init; }

        public string ParagraphId { get; init; }

        public int Page { get; init; }

        public string Ref => "r:" + Id;
    }
}
