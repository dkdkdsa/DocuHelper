using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    public static class ExcelPagePlanner
    {
        private const double PointsPerCm = 72 / 2.54;
        private const double PointsPerPixel = 0.75;
        private const double PixelsPerCharacter = 7;
        private const double DefaultColumnCharacters = 9.140625;
        private const double MinScale = 0.1;
        private const double MaxScale = 4;
        private const double Tolerance = 0.01;
        private const int MaxLines = 200000;

        public static PagePlan Plan(ExcelBook book, ExcelSheet sheet) => Plan(sheet, PageSetupModel.Read(book, sheet));

        public static PagePlan Plan(ExcelSheet sheet, PageSetupModel setup)
        {
            var paper = PaperSizes.ByKey(setup.PaperSize) ?? PaperSizes.Fallback;
            var paperWidth = setup.Landscape ? paper.HeightCm : paper.WidthCm;
            var paperHeight = setup.Landscape ? paper.WidthCm : paper.HeightCm;
            var contentWidth = Math.Max(1, paperWidth - setup.LeftCm - setup.RightCm) * PointsPerCm;
            var contentHeight = Math.Max(1, paperHeight - setup.TopCm - setup.BottomCm) * PointsPerCm;

            var areas = Areas(sheet, setup);
            var fitting = setup.FitToPage && (setup.FitWidth > 0 || setup.FitHeight > 0);
            var scale = fitting ? FitScale(sheet, setup, areas, contentWidth, contentHeight) : Math.Clamp(setup.Scale / 100.0, MinScale, MaxScale);

            var rowBreaks = fitting ? new HashSet<int>() : new HashSet<int>(setup.RowBreaks);
            var columnBreaks = fitting ? new HashSet<int>() : new HashSet<int>(setup.ColumnBreaks);
            var pages = new List<PlannedPage>();
            foreach (var area in areas)
            {
                var rows = Bands(area.Top, area.Bottom, row => RowPoints(sheet, row), contentHeight / scale, rowBreaks, setup.TitleRows);
                var columns = Bands(area.Left, area.Right, column => ColumnPoints(sheet, column), contentWidth / scale, columnBreaks, setup.TitleColumns);
                if (setup.DownThenOver)
                {
                    foreach (var column in columns)
                    {
                        foreach (var row in rows)
                            pages.Add(Page(pages.Count + 1, sheet, setup, scale, column, row));
                    }
                }
                else
                {
                    foreach (var row in rows)
                    {
                        foreach (var column in columns)
                            pages.Add(Page(pages.Count + 1, sheet, setup, scale, column, row));
                    }
                }
            }

            return new PagePlan(setup, paperWidth, paperHeight, contentWidth / PointsPerCm, contentHeight / PointsPerCm, scale, pages);
        }

        public static double ColumnPoints(ExcelSheet sheet, int column)
        {
            var width = sheet.ColumnWidth(column, out var hidden);
            if (hidden)
                return 0;

            return Math.Round((width ?? sheet.DefaultColumnWidth ?? DefaultColumnCharacters) * PixelsPerCharacter) * PointsPerPixel;
        }

        public static double RowPoints(ExcelSheet sheet, int row) =>
            sheet.IsRowHidden(row) ? 0 : sheet.RowHeight(row, out _) ?? sheet.DefaultRowHeight;

        private static List<CellRange> Areas(ExcelSheet sheet, PageSetupModel setup)
        {
            if (setup.PrintAreas.Count > 0 && !setup.PrintAreaIsFormula)
                return setup.PrintAreas.ToList();

            var used = sheet.UsedRange;
            return used == null ? new List<CellRange>() : new List<CellRange> { used };
        }

        private static double FitScale(ExcelSheet sheet, PageSetupModel setup, List<CellRange> areas, double contentWidth, double contentHeight)
        {
            var scale = 1.0;
            foreach (var area in areas)
            {
                if (setup.FitWidth > 0)
                    scale = Math.Min(scale, contentWidth * setup.FitWidth / Math.Max(1, Sum(area.Left, area.Right, column => ColumnPoints(sheet, column))));

                if (setup.FitHeight > 0)
                    scale = Math.Min(scale, contentHeight * setup.FitHeight / Math.Max(1, Sum(area.Top, area.Bottom, row => RowPoints(sheet, row))));
            }

            return Math.Clamp(Math.Floor(scale * 100) / 100, MinScale, 1);
        }

        private static double Sum(int first, int last, Func<int, double> size)
        {
            var total = 0.0;
            for (var i = first; i <= last && i - first < MaxLines; i++)
                total += size(i);

            return total;
        }

        private static List<(int First, int Last)> Bands(int first, int last, Func<int, double> size, double capacity, HashSet<int> breaks, (int First, int Last)? titles)
        {
            var bands = new List<(int, int)>();
            var titleSize = titles.HasValue ? Sum(titles.Value.First, titles.Value.Last, size) : 0;
            double Capacity(int start) => titles.HasValue && titles.Value.Last < start ? Math.Max(1, capacity - titleSize) : capacity;

            var begin = first;
            var used = 0.0;
            var limit = Capacity(begin);
            for (var i = first; i <= last && i - first < MaxLines; i++)
            {
                var length = size(i);
                if (i > begin && (breaks.Contains(i) || used + length > limit + Tolerance))
                {
                    bands.Add((begin, i - 1));
                    begin = i;
                    used = 0;
                    limit = Capacity(begin);
                }

                used += length;
            }

            bands.Add((begin, Math.Min(last, first + MaxLines - 1)));
            return bands;
        }

        private static PlannedPage Page(int number, ExcelSheet sheet, PageSetupModel setup, double scale, (int First, int Last) column, (int First, int Last) row)
        {
            var repeatsRows = setup.TitleRows.HasValue && setup.TitleRows.Value.Last < row.First;
            var repeatsColumns = setup.TitleColumns.HasValue && setup.TitleColumns.Value.Last < column.First;
            var width = Sum(column.First, column.Last, c => ColumnPoints(sheet, c));
            var height = Sum(row.First, row.Last, r => RowPoints(sheet, r));
            if (repeatsColumns)
                width += Sum(setup.TitleColumns.Value.First, setup.TitleColumns.Value.Last, c => ColumnPoints(sheet, c));

            if (repeatsRows)
                height += Sum(setup.TitleRows.Value.First, setup.TitleRows.Value.Last, r => RowPoints(sheet, r));

            return new PlannedPage(number, new CellRange(column.First, row.First, column.Last, row.Last), width * scale / PointsPerCm, height * scale / PointsPerCm, repeatsRows, repeatsColumns);
        }
    }
}
