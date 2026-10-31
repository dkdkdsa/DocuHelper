namespace DocuHelper.Tools.Word
{
    public sealed class WordCaptionInfo
    {
        public string Label { get; init; } = string.Empty;

        public string Number { get; init; } = string.Empty;

        public string Text { get; init; } = string.Empty;

        public string ParagraphId { get; init; }

        public int Page { get; init; }
    }
}
