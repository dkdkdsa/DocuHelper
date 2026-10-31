using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class SetSheetViewTool : ExcelTool
    {
        private const int MinZoom = 10;
        private const int MaxZoom = 400;
        private const uint DefaultZoom = 100;

        public override string Name => "set_sheet_view";

        public override string Description =>
            "Change how a sheet is shown, like Excel's 보기 and 시트 옵션: gridlines, row and column headings, formula display (Ctrl+`), zoom and the initial view mode "
            + "(normal, page_layout, page_break_preview). These are saved in the file. Only the properties you pass change; frozen panes and the selection are not touched.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "show_gridlines": {"type": "boolean", "description": "Show the cell gridlines."},
                "show_headings": {"type": "boolean", "description": "Show row numbers and column letters."},
                "show_formulas": {"type": "boolean", "description": "Show formulas instead of their results."},
                "zoom": {"type": "integer", "description": "Zoom in percent (10-400).", "minimum": 10, "maximum": 400},
                "view": {"type": "string", "description": "normal, page_layout or page_break_preview.", "enum": ["normal", "page_layout", "page_break_preview"]}
              },
              "required": ["sheet"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, null, true);
            var gridlines = input.GetOptionalBool("show_gridlines");
            var headings = input.GetOptionalBool("show_headings");
            var formulas = input.GetOptionalBool("show_formulas");
            var zoom = input.GetOptionalInt("zoom");
            var viewName = input.GetOptionalString("view")?.Trim().ToLowerInvariant();
            if (!gridlines.HasValue && !headings.HasValue && !formulas.HasValue && !zoom.HasValue && viewName == null)
                throw new ToolInputException("바꿀 것을 알려 주세요. (show_gridlines, show_headings, show_formulas, zoom, view)");

            if (zoom.HasValue && (zoom.Value < MinZoom || zoom.Value > MaxZoom))
                throw new ToolInputException($"확대 비율은 {MinZoom}에서 {MaxZoom} 사이로 알려 주세요. 지금 값: {zoom.Value}");

            SheetViewValues? view = null;
            if (viewName != null)
            {
                view = viewName switch
                {
                    "normal" => SheetViewValues.Normal,
                    "page_layout" => SheetViewValues.PageLayout,
                    "page_break_preview" => SheetViewValues.PageBreakPreview,
                    _ => throw new ToolInputException($"view는 normal, page_layout, page_break_preview 중에서 알려 주세요. 지금 값: {viewName}"),
                };
            }

            var sheetView = EnsureView(sheet);
            var changes = new List<(string Place, string Icon, string Text)>();

            if (gridlines.HasValue && gridlines.Value != (sheetView.ShowGridLines?.Value ?? true))
            {
                sheetView.ShowGridLines = gridlines.Value ? null : false;
                changes.Add((gridlines.Value ? "눈금선 켬" : "눈금선 끔", "grid", gridlines.Value ? "showed the gridlines" : "hid the gridlines"));
            }

            if (headings.HasValue && headings.Value != (sheetView.ShowRowColHeaders?.Value ?? true))
            {
                sheetView.ShowRowColHeaders = headings.Value ? null : false;
                changes.Add((headings.Value ? "머리글 켬" : "머리글 끔", "grid", headings.Value ? "showed the row and column headings" : "hid the row and column headings"));
            }

            if (formulas.HasValue && formulas.Value != (sheetView.ShowFormulas?.Value ?? false))
            {
                sheetView.ShowFormulas = formulas.Value ? true : null;
                changes.Add((formulas.Value ? "수식 표시" : "수식 표시 끔", "showf", formulas.Value ? "now shows formulas instead of results" : "now shows results instead of formulas"));
            }

            var currentMode = sheetView.View?.Value ?? SheetViewValues.Normal;
            var mode = view ?? currentMode;
            var effective = zoom.HasValue ? (uint)zoom.Value : ZoomOf(sheetView, mode);
            var normalZoom = mode == SheetViewValues.Normal && zoom.HasValue ? effective : ZoomOf(sheetView, SheetViewValues.Normal);
            var modeChanged = mode != currentMode;
            var zoomChanged = zoom.HasValue && effective != ZoomOf(sheetView, currentMode);
            if (modeChanged || zoomChanged || (zoom.HasValue && mode != SheetViewValues.Normal))
            {
                ApplyZoom(sheetView, mode, effective, normalZoom);
                sheetView.View = mode == SheetViewValues.Normal ? null : mode;
            }

            if (modeChanged)
            {
                var (place, text) = mode == SheetViewValues.Normal ? ("기본 보기", "normal view")
                    : mode == SheetViewValues.PageLayout ? ("페이지 레이아웃 보기", "page layout view") : ("페이지 나누기 미리 보기", "page break preview");
                changes.Add((place, "zoom", $"switched to {text}"));
            }

            if (zoomChanged)
                changes.Add(($"확대 {effective}%", "zoom", $"set the zoom to {effective}%"));

            if (changes.Count == 0)
                return ToolResult.Ok($"‘{sheet.Name}’ is already shown like that, so nothing changed.", "그대로");

            var change = new DocuChange
            {
                Location = changes.Count == 1 ? changes[0].Place : "보기 설정",
                Icon = changes[0].Icon,
                Anchors = { ExcelAnchor.Cell(sheet.Name, new CellAddress(0, 0)) },
            };
            var detail = changes.Count == 1 ? changes[0].Place : "보기 설정 바꿈";
            return ToolResult.Changed($"Sheet ‘{sheet.Name}’: {string.Join(", ", changes.Select(c => c.Text))}.", change, detail);
        }

        private static uint ZoomOf(SheetView view, SheetViewValues mode)
        {
            if (mode == SheetViewValues.Normal)
            {
                var inNormal = view.View == null || view.View.Value == SheetViewValues.Normal;
                return view.ZoomScaleNormal?.Value ?? (inNormal ? view.ZoomScale?.Value : null) ?? DefaultZoom;
            }

            var stored = mode == SheetViewValues.PageLayout ? view.ZoomScalePageLayoutView?.Value : view.ZoomScaleSheetLayoutView?.Value;
            return stored ?? DefaultZoom;
        }

        private static void ApplyZoom(SheetView view, SheetViewValues mode, uint zoom, uint normalZoom)
        {
            static uint? Omit(uint value) => value == DefaultZoom ? null : value;

            view.ZoomScale = Omit(zoom);
            view.ZoomScaleNormal = Omit(normalZoom);
            view.ZoomScalePageLayoutView = mode == SheetViewValues.PageLayout ? Omit(zoom) : view.ZoomScalePageLayoutView;
            view.ZoomScaleSheetLayoutView = mode == SheetViewValues.PageBreakPreview ? Omit(zoom) : view.ZoomScaleSheetLayoutView;
        }

        private static SheetView EnsureView(ExcelSheet sheet)
        {
            var worksheet = sheet.Worksheet;
            var views = worksheet.SheetViews ?? WorksheetOrder.Insert(worksheet, new SheetViews());
            var view = views.Elements<SheetView>().FirstOrDefault();
            if (view == null)
            {
                view = new SheetView { WorkbookViewId = 0 };
                views.Append(view);
            }

            return view;
        }
    }
}
