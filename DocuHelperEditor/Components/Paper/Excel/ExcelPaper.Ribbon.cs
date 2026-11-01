using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;
using DocuHelperEditor.State.Ribbon.Excel;
using Microsoft.AspNetCore.Components;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper : IExcelSurface
    {
        private ExcelCommandRouter _router;
        private Painter _painter;

        [Inject]
        private IFileDialogService FileDialogs { get; set; }

        ExcelPaperModel IExcelSurface.Model => Model;

        Sheet IExcelSurface.Sheet => CurrentSheet;

        SheetRegion IExcelSurface.Selection => Selection;

        string IExcelSurface.ActiveRef => SelectedRef;

        SheetCell IExcelSurface.ActiveCell => SelectedCell;

        SheetRowKind IExcelSurface.ActiveRowKind => SelectedRowKind;

        bool IExcelSurface.IsEditing => IsEditing;

        bool IExcelSurface.IsMergedSelection => IsMergedSelection;

        bool IExcelSurface.WholeRows => WholeRows;

        bool IExcelSurface.WholeColumns => WholeColumns;

        bool IExcelSurface.ShowFormulaBar
        {
            get => Model.View.ShowFormulaBar;
            set => Model.View.ShowFormulaBar = value;
        }

        bool IExcelSurface.HasClip => _clip != null;

        string IExcelSurface.ClipSheet => _clip?.Sheet;

        string IExcelSurface.ClipRef => _clip?.Region.ToRef();

        bool IExcelSurface.ClipIsCut => _clip?.IsCut == true;

        bool IExcelSurface.IsPainting => _painter != null;

        private string PaintingClass => _painter != null ? "painting" : null;

        protected override void OnInitialized()
        {
            _router = new ExcelCommandRouter(Workspace, Assistant, JS, Clipboards, FileDialogs, this);
            _router.File = File;
            _router.Changed += OnRouterChanged;
            Clipboards.Changed += OnRouterChanged;
#if DEBUG
            var missing = new List<string>();
            for (var level = 1; level <= ExcelRibbonDefinition.MaxLevel; level++)
                missing.AddRange(_router.MissingIds(ExcelRibbonDefinition.Tabs(level), ExcelRibbonDefinition.FileItems));

            if (missing.Count > 0)
                AppLog.Write("Excel ribbon commands missing: " + string.Join(", ", missing.Distinct()));
#endif
        }

        protected override void OnDispose()
        {
            if (_router == null)
                return;

            _router.Changed -= OnRouterChanged;
            Clipboards.Changed -= OnRouterChanged;
        }

        private void OnRouterChanged() => _ = InvokeAsync(StateHasChanged);

        bool IExcelSurface.Prepare()
        {
            _confirm = null;
            return !IsEditing || CommitEdit();
        }

        void IExcelSurface.Refocus() => _focusSink = true;

        void IExcelSurface.Message(string text) => _editError = text;

        void IExcelSurface.Confirm(string text, string button, Action run) => _confirm = new Confirm(text, button, run);

        void IExcelSurface.Select(SheetRegion region) => SelectRegion(region);

        string IExcelSurface.NextSheetName() => NextSheetName();

        Task IExcelSurface.RunCommandAsync(string command) => RunCommandAsync(command);

        Task IExcelSurface.CopyAsync(bool cut) => CopyAsync(cut);

        Task IExcelSurface.PasteAsync() => PasteFromClipboardAsync();

        void IExcelSurface.SortTable(bool descending) => SortTable(descending);

        void IExcelSurface.AutoSum(string function)
        {
            if (function == "more")
                BeginFormula("=");
            else
                AutoSum(function);
        }

        void IExcelSurface.ToggleMerge()
        {
            _focusSink = true;
            ToggleMerge();
        }

        void IExcelSurface.OpenFind(bool replace) => OpenFind(replace);

        void IExcelSurface.FocusNameBox() => _ = RunJsAsync("docuHelper.selectText", "#" + RefId);

        void IExcelSurface.BeginFormula(string text) => BeginFormula(text);

        void IExcelSurface.BeginSheetRename(int index) => _tabs?.BeginRename(index);

        bool IExcelSurface.SelectNext(string kind) => SelectNext(kind);

        void IExcelSurface.ArmPainter() => _painter = new Painter(CurrentSheet.Name, Selection.ToRef());

        void IExcelSurface.DisarmPainter() => _painter = null;

        void IExcelSurface.AfterPaste(bool transposed)
        {
            if (_clip == null)
                return;

            var target = Selection;
            var rows = transposed ? _clip.Region.ColumnCount : _clip.Region.RowCount;
            var columns = transposed ? _clip.Region.RowCount : _clip.Region.ColumnCount;
            if (_clip.IsCut)
                _clip = null;

            SelectRegion(new SheetRegion(target.Top, target.Left, Math.Min(MaxRow, target.Top + rows - 1), Math.Min(MaxColumn, target.Left + columns - 1)));
        }

        private void BeginFormula(string text)
        {
            if (IsLocked)
                return;

            if (IsEditing && !CommitEdit())
                return;

            BeginCellEdit(false);
            _draft = text;
            _sinkSeed = text;
        }

        private void ApplyPainter()
        {
            var painter = _painter;
            _painter = null;
            if (painter == null)
                return;

            var target = Selection;
            if (_router.ApplyPainter(painter.Sheet, painter.Range, SheetRef.Make(target.Left, target.Top)))
                SelectRegion(target);
        }

        private bool PasteWithTool()
        {
            var handled = _router.PasteOwnClip();
            if (handled.HasValue && handled.Value)
                ((IExcelSurface)this).AfterPaste(false);

            return handled.HasValue;
        }

        private bool SelectNext(string kind)
        {
            var sheet = CurrentSheet;
            var found = new List<(int Row, int Column)>();
            var columns = Math.Max(0, sheet.Columns.Count - 1);
            var rows = sheet.Rows.Count;
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < columns; c++)
                {
                    if (Matches(kind, sheet.FindCell(r, c)))
                        found.Add((r, c));
                }
            }

            var label = SelectKindLabel(kind);
            if (found.Count == 0)
            {
                _editError = label + " 칸이 없어요.";
                return false;
            }

            SheetRef.TryParse(SelectedRef, out var column, out var row);
            var next = found.FindIndex(m => m.Row > row || (m.Row == row && m.Column > column));
            if (next < 0)
                next = 0;

            var (targetRow, targetColumn) = found[next];
            SelectRegion(new SheetRegion(targetRow, targetColumn, targetRow, targetColumn));
            _scrollTarget = "#" + SinkId;
            _editError = $"{label} 칸 {found.Count}개 중 {next + 1}번째예요.";
            return true;
        }

        private static bool Matches(string kind, SheetCell cell)
        {
            switch (kind)
            {
                case "blank":
                    return !SheetRegion.HasValue(cell);
                case "formula":
                    return cell != null && !string.IsNullOrEmpty(cell.Formula);
                case "constant":
                    return SheetRegion.HasValue(cell) && string.IsNullOrEmpty(cell.Formula);
                case "note":
                    return cell?.Note != null;
                case "conditional":
                    return cell?.Visual != null;
                case "validation":
                    return cell?.ValidationIndex != null;
                case "error":
                    return cell != null && !cell.IsNumber && !string.IsNullOrEmpty(cell.Text) && cell.Text.StartsWith('#') && cell.Text.Length > 1;
                default:
                    return false;
            }
        }

        private static string SelectKindLabel(string kind) => kind switch
        {
            "blank" => "비어 있는",
            "formula" => "수식이 있는",
            "constant" => "값만 있는",
            "note" => "메모가 있는",
            "conditional" => "조건부 서식이 있는",
            "validation" => "유효성 검사가 있는",
            "error" => "오류가 난",
            _ => "찾는",
        };

        private sealed record Painter(string Sheet, string Range);
    }
}
