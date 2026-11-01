using System;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private void GrowRows() => GrowRowsTo(CurrentSheet.DisplayRowCount - 1);

        private void GrowRowsTo(int row)
        {
            var sheet = CurrentSheet;
            if (row < sheet.DisplayRowCount - 1 || sheet.DisplayRowCount >= ExcelViewState.MaxRows)
                return;

            var limit = Math.Min(ExcelViewState.MaxRows, Math.Max(sheet.DisplayRowCount, row + 1) + ExcelViewState.RowStep);
            Model.View.SetRowLimit(sheet.Name, limit);
            sheet.RowLimit = Model.View.RowLimitOf(sheet.Name);
        }
    }
}
