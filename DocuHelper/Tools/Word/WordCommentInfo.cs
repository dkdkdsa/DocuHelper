using System;

namespace DocuHelper.Tools.Word
{
    public sealed class WordCommentInfo
    {
        public string Id { get; init; }

        public string Author { get; init; }

        public string Initials { get; init; }

        public DateTime? Date { get; init; }

        public string Anchored { get; init; } = string.Empty;

        public string Text { get; init; } = string.Empty;

        public bool Done { get; init; }

        public string ParentId { get; init; }

        public string ParagraphId { get; init; }

        public int Page { get; init; }

        public string Ref => "c:" + Id;

        public string Anchor => WordAnchors.Comment(Id);
    }
}
