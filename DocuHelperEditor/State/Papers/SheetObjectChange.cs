namespace DocuHelperEditor.State.Papers
{
    public enum SheetObjectAction
    {
        Move,
        Resize,
        Delete,
    }

    public sealed record SheetObjectChange(string SheetName, string Id, string Name, SheetObjectAction Action, int Row, int Column, int Columns, int Rows);
}
