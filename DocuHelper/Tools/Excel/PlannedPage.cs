namespace DocuHelper.Tools.Excel
{
    public sealed class PlannedPage
    {
        public PlannedPage(int number, CellRange cells, double widthCm, double heightCm, bool repeatsTitleRows, bool repeatsTitleColumns)
        {
            Number = number;
            Cells = cells;
            WidthCm = widthCm;
            HeightCm = heightCm;
            RepeatsTitleRows = repeatsTitleRows;
            RepeatsTitleColumns = repeatsTitleColumns;
        }

        public int Number { get; }

        public CellRange Cells { get; }

        public double WidthCm { get; }

        public double HeightCm { get; }

        public bool RepeatsTitleRows { get; }

        public bool RepeatsTitleColumns { get; }
    }
}
