namespace DocuHelper.Tools.Word
{
    public sealed class WordNoteInfo
    {
        public bool IsEndnote { get; init; }

        public int Id { get; init; }

        public int Number { get; init; }

        public string Text { get; init; } = string.Empty;

        public string ParagraphId { get; init; }

        public int Page { get; init; }

        public string Ref => (IsEndnote ? WordAnchors.EndnotePrefix : WordAnchors.FootnotePrefix) + Id;
    }
}
