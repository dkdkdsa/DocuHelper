using System;
using DocuHelper.Docus;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private Sheet _planSheet;
        private PagePlan _pagePlan;

        private PagePlan CurrentPagePlan
        {
            get
            {
                if (ViewModeValue == SheetViewMode.Normal || File?.Docu is not ExcelDocu excel)
                    return null;

                if (!ReferenceEquals(_planSheet, CurrentSheet))
                {
                    _planSheet = CurrentSheet;
                    _pagePlan = BuildPagePlan(excel, CurrentSheet.Name);
                }

                return _pagePlan;
            }
        }

        private static PagePlan BuildPagePlan(ExcelDocu excel, string sheetName)
        {
            try
            {
                var book = new ExcelBook(excel.Document);
                var sheet = book.FindSheet(sheetName);
                return sheet == null ? null : ExcelPagePlanner.Plan(book, sheet);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                AppLog.Write("Could not plan the pages", e);
                return null;
            }
        }
    }
}
