using DocuHelper.Docus;
using DocuHelper.Tools.Excel;
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
                    _pagePlan = ExcelPagePlans.Build(excel, CurrentSheet.Name);
                }

                return _pagePlan;
            }
        }
    }
}
