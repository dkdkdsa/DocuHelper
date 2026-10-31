namespace DocuHelperEditor.State.Papers
{
    public sealed record SheetMenuItem(string Key, string Label, bool Disabled = false, bool Separator = false);
}
