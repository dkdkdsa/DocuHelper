using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public sealed class ExcelViewState
    {
        public const int MinZoom = 50;
        public const int MaxZoom = 200;

        private readonly Dictionary<string, SheetView> _sheets = new(StringComparer.Ordinal);

        public bool ShowRuler { get; set; } = true;

        public bool ShowFormulaBar { get; set; } = true;

        public string ViewModeOf(string sheet) => Of(sheet).Mode;

        public void SetViewMode(string sheet, string mode) => Of(sheet).Mode = mode;

        public int ZoomOf(string sheet) => Of(sheet).Zoom;

        public void SetZoom(string sheet, int zoom) => Of(sheet).Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);

        private SheetView Of(string sheet)
        {
            if (!_sheets.TryGetValue(sheet, out var view))
            {
                view = new SheetView();
                _sheets[sheet] = view;
            }

            return view;
        }

        private sealed class SheetView
        {
            public string Mode { get; set; } = SheetViewMode.Normal;

            public int Zoom { get; set; } = 100;
        }
    }
}
