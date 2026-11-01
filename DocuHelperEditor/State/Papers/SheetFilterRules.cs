namespace DocuHelperEditor.State.Papers
{
    public static class SheetFilterRules
    {
        public static SheetRegion? RegionAt(Sheet sheet, int row, int column)
        {
            if (sheet == null)
                return null;

            if (sheet.Filter != null && sheet.Filter.IsHeaderCell(row, column))
                return sheet.Filter.Range;

            var table = sheet.FindTable(row, column);
            if (table != null && table.FilterButtons && table.IsHeaderCell(row, column))
                return table.Range;

            return null;
        }
    }
}
