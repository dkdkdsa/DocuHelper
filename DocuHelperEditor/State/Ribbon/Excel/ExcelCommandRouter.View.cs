using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DocuHelper.Tools;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter
    {
        private const string ZoomSelectionFeature = "zoom-selection";

        private static readonly int[] s_zoomLevels = { 200, 150, 100, 75, 50 };

        private void RegisterView()
        {
            State.RegisterFeature(ExcelRibbonState.ZoomFeature);
            State.RegisterFeature(ExcelRibbonState.RulerFeature);
            ViewMode("excel.view.normal", ExcelRibbonState.NormalView);
            ViewMode("excel.view.pagebreak", ExcelRibbonState.PageBreakView);
            ViewMode("excel.view.pagelayout", ExcelRibbonState.PageLayoutView);
            Gated(AddView("excel.view.ruler", _ => State.ShowRuler = !State.ShowRuler), ExcelRibbonState.RulerFeature)
                .PressedWhen(() => State.ShowRuler);
            AddView("excel.view.formulabar", _ => _surface.ShowFormulaBar = !_surface.ShowFormulaBar)
                .PressedWhen(() => _surface.ShowFormulaBar);
            SheetOption("excel.view.gridlines", "set_sheet_view", "show_gridlines", () => State.Sheet.ShowGridlines);
            SheetOption("excel.view.headings", "set_sheet_view", "show_headings", () => State.Sheet.ShowHeadings);
            Gated(AddView("excel.view.zoom", arg => SetZoom(arg)), ExcelRibbonState.ZoomFeature)
                .MenuFrom(ZoomMenu);
            Gated(AddView("excel.view.zoom100", _ => State.Zoom = 100), ExcelRibbonState.ZoomFeature);
            Gated(AddView("excel.view.zoomselection", _ => ZoomToSelection()), ZoomSelectionFeature);
            AddFile("excel.view.freeze", arg => RunFreeze(arg))
                .Needs("freeze_panes")
                .PressedWhen(() => State.IsFrozen)
                .MenuFrom(FreezeMenu);
            AddExternal("excel.view.newwindow");
            AddDisabled("excel.view.arrangeall", "이 앱은 파일마다 탭으로 열어서 창을 나란히 둘 수 없어요.");
            AddDisabled("excel.view.split", "화면 나누기는 아직 지원하지 않아요. 틀 고정을 써 보세요.");
            AddDisabled("excel.view.hidewindow", "이 앱에는 창을 숨기는 기능이 없어요.");
            RegisterBars();
        }

        private void RegisterBars()
        {
            Alias("excel.fbar.insertfn", "excel.formula.insertfn");
            Alias("excel.status.view.normal", "excel.view.normal");
            Alias("excel.status.view.pagelayout", "excel.view.pagelayout");
            Alias("excel.status.view.pagebreak", "excel.view.pagebreak");
            Gated(AddView("excel.status.zoomout", _ => State.Zoom -= 10), ExcelRibbonState.ZoomFeature);
            Gated(AddView("excel.status.zoomin", _ => State.Zoom += 10), ExcelRibbonState.ZoomFeature);
            Gated(AddView("excel.status.zoomreset", _ => State.Zoom = 100), ExcelRibbonState.ZoomFeature);
            Gated(AddView("excel.status.zoomslider", arg => SetZoom(arg)), ExcelRibbonState.ZoomFeature);
        }

        private void ViewMode(string id, string mode)
        {
            var command = AddView(id, _ => State.ViewMode = mode)
                .PressedWhen(() => State.ViewMode == mode);
            if (mode != ExcelRibbonState.NormalView)
                Gated(command, ExcelRibbonState.ViewModesFeature);
        }

        private IReadOnlyList<RibbonMenuItem> ZoomMenu()
        {
            var items = new List<RibbonMenuItem>();
            foreach (var level in s_zoomLevels)
                items.Add(Entry(level.ToString(CultureInfo.InvariantCulture), level + "%", check: State.Zoom == level));

            items.Add(RibbonMenus.Separator());
            items.Add(RibbonMenus.Input("직접 입력", State.Zoom.ToString(CultureInfo.InvariantCulture), "50부터 200까지"));
            return items;
        }

        private void SetZoom(string arg)
        {
            var text = (arg ?? string.Empty).Trim().TrimEnd('%').Trim();
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var zoom) || zoom < 50 || zoom > 200)
                throw new ToolInputException("확대/축소는 50부터 200 사이의 숫자로 적어 주세요.");

            State.Zoom = zoom;
        }

        private void ZoomToSelection()
        {
            State.Zoom = Math.Min(200, State.Zoom + 50);
        }

        private IReadOnlyList<RibbonMenuItem> FreezeMenu()
        {
            return new[]
            {
                Entry(SheetCommands.FreezeHere, "틀 고정", "고른 칸 위쪽 줄과 왼쪽 열이 늘 보여요"),
                Entry(SheetCommands.FreezeRow, "첫 행 고정"),
                Entry(SheetCommands.FreezeColumn, "첫 열 고정"),
                Entry(SheetCommands.Unfreeze, "틀 고정 취소", blocked: State.IsFrozen ? null : "고정한 틀이 없어요."),
            };
        }

        private Task RunFreeze(string arg)
        {
            return string.IsNullOrEmpty(arg) ? Task.CompletedTask : _surface.RunCommandAsync(arg);
        }
    }
}
