using System.Threading.Tasks;
using DocuHelper.Docus;
using Microsoft.JSInterop;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private Sheet _planSheet;
        private PagePlan _pagePlan;
        private bool _renderAllRows;

        public async Task RenderAllRowsAsync(bool on)
        {
            if (_renderAllRows == on)
                return;

            _renderAllRows = on;
            await InvokeAsync(StateHasChanged);
            await JS.InvokeVoidAsync("docuHelper.nextFrame");
        }

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
