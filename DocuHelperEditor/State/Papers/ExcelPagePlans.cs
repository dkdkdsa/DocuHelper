using System;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.State.Papers
{
    public sealed record PagePlanMetrics(PagePlan Plan, double[] RowPoints, double[] ColumnPoints);

    public static class ExcelPagePlans
    {
        public static PagePlan Build(ExcelDocu excel, string sheetName) => Measure(excel, sheetName)?.Plan;

        public static PagePlanMetrics Measure(ExcelDocu excel, string sheetName)
        {
            try
            {
                var book = new ExcelBook(excel.Document);
                var sheet = book.FindSheet(sheetName);
                if (sheet == null)
                    return null;

                var plan = ExcelPagePlanner.Plan(book, sheet);
                var rows = plan.Pages.Count == 0 ? 0 : plan.Pages.Max(page => page.Cells.Bottom) + 1;
                var columns = plan.Pages.Count == 0 ? 0 : plan.Pages.Max(page => page.Cells.Right) + 1;
                var rowPoints = Enumerable.Range(0, rows).Select(row => ExcelPagePlanner.RowPoints(sheet, row)).ToArray();
                var columnPoints = Enumerable.Range(0, columns).Select(column => ExcelPagePlanner.ColumnPoints(sheet, column)).ToArray();
                return new PagePlanMetrics(plan, rowPoints, columnPoints);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                AppLog.Write("Could not plan the pages", e);
                return null;
            }
        }
    }
}
