namespace DocuHelper.Tools.Word
{
    public sealed record WordAnchor(WordAnchorKind Kind, string Id, int Number, bool HasRange, int Start, int End)
    {
        public string Text => WordAnchors.Format(this);
    }
}
