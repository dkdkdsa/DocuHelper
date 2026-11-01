using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed class ExcelRibbonState
    {
        public const string DefaultFontColor = "C0392B";
        public const string DefaultFillColor = "FFE35A";

        private static readonly double[] s_ladder = { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };

        private readonly IExcelSurface _surface;
        private readonly HashSet<string> _dialogs = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _features = new HashSet<string>(StringComparer.Ordinal);

        public ExcelRibbonState(IExcelSurface surface)
        {
            _surface = surface;
        }

        public const string NormalView = SheetViewMode.Normal;
        public const string PageLayoutView = SheetViewMode.PageLayout;
        public const string PageBreakView = SheetViewMode.PageBreak;
        public const string ViewModesFeature = "view-modes";
        public const string ZoomFeature = "zoom";
        public const string RulerFeature = "ruler";

        public string FontColor { get; set; } = DefaultFontColor;

        public string FillColor { get; set; } = DefaultFillColor;

        public string ActiveDialog { get; private set; }

        public bool ShowRuler
        {
            get => Model.View.ShowRuler;
            set => Model.View.ShowRuler = value;
        }

        public string ViewMode
        {
            get => Model.View.ViewModeOf(SheetName);
            set => Model.View.SetViewMode(SheetName, value);
        }

        public int Zoom
        {
            get => Model.View.ZoomOf(SheetName);
            set => Model.View.SetZoom(SheetName, value);
        }

        public bool HasDialog(string id) => _dialogs.Contains(id);

        public void RegisterDialog(string id) => _dialogs.Add(id);

        public bool HasFeature(string feature) => _features.Contains(feature);

        public void RegisterFeature(string feature) => _features.Add(feature);

        public void OpenDialog(string id) => ActiveDialog = HasDialog(id) ? id : null;

        public void CloseDialog() => ActiveDialog = null;

        public ExcelPaperModel Model => _surface.Model;

        public Sheet Sheet => _surface.Sheet;

        public string SheetName => _surface.Sheet.Name;

        public SheetRegion Selection => _surface.Selection;

        public string Range => _surface.Selection.ToRef();

        public string ActiveRef => _surface.ActiveRef;

        public SheetCell Cell => _surface.ActiveCell;

        public bool IsMerged => _surface.IsMergedSelection;

        public bool IsFrozen => Sheet.FreezeRows > 0 || Sheet.FreezeColumns > 0;

        public bool IsFiltered => Sheet.Filter != null;

        public bool IsInTable => Sheet.FindTable(Active.Row, Active.Column) != null;

        public bool IsBold => Cell != null ? Cell.IsBoldIn(_surface.ActiveRowKind) : _surface.ActiveRowKind != SheetRowKind.Normal;

        public bool IsItalic => Cell?.Italic == true;

        public bool IsUnderline => Cell?.Underline == true;

        public bool IsWrapped => Cell?.Wrap == true;

        public bool IsLocked => Cell?.Locked ?? true;

        public SheetAlign Align => Cell?.Align ?? SheetAlign.Auto;

        public string VerticalAlign => string.IsNullOrEmpty(Cell?.VAlign) ? "bottom" : Cell.VAlign;

        public string FontName => Cell?.FontFamily ?? Model.DefaultFontName ?? "맑은 고딕";

        public double FontSize => Cell?.FontSize ?? Model.DefaultFontSize;

        public (int Row, int Column) Active
        {
            get
            {
                SheetRef.TryParse(_surface.ActiveRef, out var column, out var row);
                return (row, column);
            }
        }

        public string NumberCategory
        {
            get
            {
                if (!string.IsNullOrEmpty(Cell?.NumberCategory))
                    return Cell.NumberCategory;

                return Cell?.NumberFormat switch
                {
                    SheetNumberFormat.Percent => "백분율",
                    SheetNumberFormat.Thousands => "숫자",
                    _ => "일반",
                };
            }
        }

        public string Region
        {
            get
            {
                var selection = _surface.Selection;
                if (!selection.IsSingle)
                    return selection.ToRef();

                var (row, column) = Active;
                return SheetRegion.Around(Sheet, column, row)?.ToRef() ?? selection.ToRef();
            }
        }

        public string Rows => LineRef(true, _surface.Selection.Top, _surface.Selection.Bottom);

        public string Columns => LineRef(false, _surface.Selection.Left, _surface.Selection.Right);

        public string ActiveColumn => SheetRef.ColumnName(Active.Column);

        public int ActiveRow => Active.Row + 1;

        public bool HasNotes => Sheet.NoteCells.Any();

        public bool HasFilterCriteria => Sheet.Filter?.IsActive == true;

        public bool SelectionHasText
        {
            get
            {
                var region = _surface.Selection;
                for (var r = region.Top; r <= region.Bottom; r++)
                {
                    for (var c = region.Left; c <= region.Right; c++)
                    {
                        var cell = Sheet.FindCell(r, c);
                        if (SheetRegion.HasValue(cell) && !cell.IsNumber)
                            return true;
                    }
                }

                return false;
            }
        }

        public double NextLarger()
        {
            var size = FontSize;
            foreach (var step in s_ladder)
            {
                if (step > size + 0.001)
                    return step;
            }

            return Math.Min(409, size + 1);
        }

        public double NextSmaller()
        {
            var size = FontSize;
            for (var i = s_ladder.Length - 1; i >= 0; i--)
            {
                if (s_ladder[i] < size - 0.001)
                    return s_ladder[i];
            }

            return Math.Max(1, size - 1);
        }

        public static string SizeText(double size) => size.ToString("0.#", CultureInfo.InvariantCulture);

        public static string LineRef(bool rows, int first, int last) => rows
            ? (first + 1).ToString(CultureInfo.InvariantCulture) + ":" + (last + 1).ToString(CultureInfo.InvariantCulture)
            : SheetRef.ColumnName(first) + ":" + SheetRef.ColumnName(last);
    }
}
