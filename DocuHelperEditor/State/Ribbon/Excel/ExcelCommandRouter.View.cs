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
        private const double ZoomViewportWidth = 720;
        private const double ZoomViewportHeight = 440;
        private const double ZoomOpenColumnPx = 120;
        private const int ZoomMaxRows = 500;
        private const int ZoomMaxColumns = 50;

        private static readonly int[] s_zoomLevels = { 200, 150, 100, 75, 50 };

        private void RegisterFeatures()
        {
            State.RegisterFeature(ExcelRibbonState.ZoomFeature);
            State.RegisterFeature(ExcelRibbonState.RulerFeature);
            State.RegisterFeature(ExcelRibbonState.ViewModesFeature);
            State.RegisterFeature(ZoomSelectionFeature);
        }

        private void AddViewHandlers()
        {
            _runs.Add("viewNormal", Run(_ => State.ViewMode = ExcelRibbonState.NormalView));
            _runs.Add("viewPageBreak", Run(_ => State.ViewMode = ExcelRibbonState.PageBreakView));
            _runs.Add("viewPageLayout", Run(_ => State.ViewMode = ExcelRibbonState.PageLayoutView));
            _runs.Add("ruler", Run(_ => State.ShowRuler = !State.ShowRuler));
            _runs.Add("formulaBar", Run(_ => _surface.ShowFormulaBar = !_surface.ShowFormulaBar));
            _runs.Add("setZoom", Run(SetZoom));
            _runs.Add("zoomReset", Run(_ => State.Zoom = 100));
            _runs.Add("zoomOut", Run(_ => State.Zoom -= 10));
            _runs.Add("zoomIn", Run(_ => State.Zoom += 10));
            _runs.Add("zoomSelection", Run(_ => ZoomToSelection()));
            _runs.Add("freeze", RunFreeze);

            _pressed.Add("viewNormal", () => State.ViewMode == ExcelRibbonState.NormalView);
            _pressed.Add("viewPageBreak", () => State.ViewMode == ExcelRibbonState.PageBreakView);
            _pressed.Add("viewPageLayout", () => State.ViewMode == ExcelRibbonState.PageLayoutView);
            _pressed.Add("ruler", () => State.ShowRuler);
            _pressed.Add("formulaBar", () => _surface.ShowFormulaBar);
            _pressed.Add("frozen", () => State.IsFrozen);

            _needs.Add("viewModes", _ => Feature(ExcelRibbonState.ViewModesFeature));
            _needs.Add("rulerFeature", _ => Feature(ExcelRibbonState.RulerFeature));
            _needs.Add("zoomFeature", _ => Feature(ExcelRibbonState.ZoomFeature));
            _needs.Add("zoomSelectionFeature", _ => Feature(ZoomSelectionFeature));

            _menus.Add("zoom", _ => ZoomMenu());
            _menus.Add("freeze", _ => FreezeMenu());
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
            var region = State.Selection;
            if (region.Bottom - region.Top >= ZoomMaxRows || region.Right - region.Left >= ZoomMaxColumns)
            {
                State.Zoom = ExcelViewState.MinZoom;
                return;
            }

            var width = 0.0;
            for (var c = region.Left; c <= region.Right; c++)
            {
                var column = c < State.Sheet.Columns.Count ? State.Sheet.Columns[c] : null;
                if (column?.Hidden != true)
                {
                    width += column?.Width ?? ZoomOpenColumnPx;
                }
            }

            var height = 0.0;
            for (var r = region.Top; r <= region.Bottom; r++)
            {
                var row = State.Sheet.FindRow(r);
                if (row?.Hidden != true)
                {
                    height += row?.Height ?? State.Sheet.DefaultRowHeight;
                }
            }

            var fit = Math.Min(ZoomViewportWidth / Math.Max(width, 1), ZoomViewportHeight / Math.Max(height, 1));
            State.Zoom = (int)Math.Floor(fit * 100 / 5) * 5;
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
