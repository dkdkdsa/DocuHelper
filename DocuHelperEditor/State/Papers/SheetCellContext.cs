namespace DocuHelperEditor.State.Papers
{
    public readonly record struct SheetCellContext(Sheet Sheet, SheetRow Row, SheetCell Cell, int RowIndex, int ColumnIndex, SheetRegion Area, bool Active)
    {
        public string Ref => SheetRef.Make(Area.Left, Area.Top);
    }
}
