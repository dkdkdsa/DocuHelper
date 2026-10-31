namespace DocuHelperEditor.State.Ribbon
{
    public sealed record RibbonFileItem
    {
        public string Id { get; init; }

        public string Label { get; init; }

        public string Shortcut { get; init; }

        public bool Separated { get; init; }
    }
}
