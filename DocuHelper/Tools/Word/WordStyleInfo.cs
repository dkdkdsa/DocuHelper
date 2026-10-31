namespace DocuHelper.Tools.Word
{
    public sealed class WordStyleInfo
    {
        public string Id { get; init; }

        public string Name { get; init; }

        public string Type { get; init; }

        public string BasedOn { get; init; }

        public int Users { get; init; }

        public bool IsDefault { get; init; }

        public bool IsCustom { get; init; }

        public string Summary { get; init; } = string.Empty;
    }
}
