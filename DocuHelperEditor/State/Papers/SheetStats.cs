namespace DocuHelperEditor.State.Papers
{
    public sealed record SheetStatsResult(int Count, int Numbers, decimal Sum)
    {
        public decimal Average => Numbers == 0 ? 0 : Sum / Numbers;
    }

    public static class SheetStats
    {
        public const int MaxCells = 100000;

        public static SheetStatsResult Of(Sheet sheet, SheetRegion region)
        {
            if (sheet == null || (long)region.RowCount * region.ColumnCount > MaxCells)
                return null;

            var count = 0;
            var numbers = 0;
            var sum = 0m;
            for (var row = region.Top; row <= region.Bottom; row++)
            {
                for (var column = region.Left; column <= region.Right; column++)
                {
                    var cell = sheet.FindCell(row, column);
                    if (!SheetRegion.HasValue(cell))
                        continue;

                    count++;
                    if (cell.IsNumber && SheetNumber.TryParse(cell.Text, out var value, out _))
                    {
                        numbers++;
                        sum += value;
                    }
                }
            }

            return new SheetStatsResult(count, numbers, sum);
        }
    }
}
