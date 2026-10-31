namespace DocuHelperEditor.State.Papers
{
    public readonly record struct SheetObjectAnchor(int FromRow, int FromColumn, double FromRowOffset, double FromColumnOffset, int ToRow, int ToColumn, double ToRowOffset, double ToColumnOffset);
}
