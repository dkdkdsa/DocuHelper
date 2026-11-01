namespace DocuHelperEditor.State.Papers
{
    public sealed record SheetObjectChange(string Name, string Action, int Row, int Column, int Columns, int Rows);
}
