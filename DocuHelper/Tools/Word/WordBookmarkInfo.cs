namespace DocuHelper.Tools.Word
{
    public sealed class WordBookmarkInfo
    {
        public string Name { get; init; }

        public string Id { get; init; }

        public string Text { get; init; } = string.Empty;

        public string ParagraphId { get; init; }

        public int Page { get; init; }

        public bool Hidden => Name != null && Name.StartsWith("_", System.StringComparison.Ordinal);
    }
}
