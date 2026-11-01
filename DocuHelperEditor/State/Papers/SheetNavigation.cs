namespace DocuHelperEditor.State.Papers
{
    public static class SheetNavigation
    {
        public static (int Row, int Column) Jump(Sheet sheet, int row, int column, int rowStep, int columnStep)
        {
            var maxRow = ExcelViewState.MaxRows - 1;
            var maxColumn = sheet.Columns.Count - 1;
            bool Inside(int r, int c) => r >= 0 && c >= 0 && r <= maxRow && c <= maxColumn;
            bool Filled(int r, int c) => SheetRegion.HasValue(sheet.FindCell(r, c));

            var nextRow = row + rowStep;
            var nextColumn = column + columnStep;
            if (!Inside(nextRow, nextColumn))
                return (row, column);

            if (Filled(row, column) && Filled(nextRow, nextColumn))
            {
                while (Inside(nextRow + rowStep, nextColumn + columnStep) && Filled(nextRow + rowStep, nextColumn + columnStep))
                {
                    nextRow += rowStep;
                    nextColumn += columnStep;
                }

                return (nextRow, nextColumn);
            }

            while (!Filled(nextRow, nextColumn))
            {
                if (!Inside(nextRow + rowStep, nextColumn + columnStep))
                    return (nextRow, nextColumn);

                nextRow += rowStep;
                nextColumn += columnStep;
            }

            return (nextRow, nextColumn);
        }
    }
}
