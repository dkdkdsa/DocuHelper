using System.Globalization;
using System.Threading.Tasks;
using DocuHelperEditor.State.Papers;
using DocuHelperEditor.State.Ribbon.Excel;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private bool ShowFormulaBar => Model.View.ShowFormulaBar;

        private SheetStatsResult SelectionStats => Selection.IsSingle ? null : SheetStats.Of(CurrentSheet, Selection);

        private string StatusText => IsLocked ? "도우미가 고치는 중" : IsEditing ? "편집" : "준비";

        private int ZoomValue => _router.State.Zoom;

        private string ViewModeValue => _router.State.ViewMode;

        private bool PageModesReady => _router.State.HasFeature(ExcelRibbonState.ViewModesFeature);

        private Task RunStatusCommandAsync(string id) => _router.InvokeAsync(id, null);

        private Task InsertFunctionAsync() => _router.InvokeAsync("excel.fbar.insertfn", null);

        private Task SetZoomAsync(int value) => _router.InvokeAsync("excel.status.zoomslider", value.ToString(CultureInfo.InvariantCulture));
    }
}
