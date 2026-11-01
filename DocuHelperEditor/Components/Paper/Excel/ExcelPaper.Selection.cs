using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.Services;
using DocuHelperEditor.State;
using DocuHelperEditor.State.Papers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private void SelectCell(string cellRef, bool focus, bool extend = false)
        {
            if (focus)
                _focusSink = true;

            var sheet = CurrentSheet;
            if (extend)
            {
                if (IsEditing && !CommitEdit())
                    return;

                sheet = CurrentSheet;
                sheet.ExtentRef = cellRef == SelectedRef ? null : cellRef;
                if (sheet.ExtentRef != null)
                {
                    _scrollTarget = "#" + _uid + " [data-x]";
                    if (SheetRef.TryParse(cellRef, out _, out var extentRow))
                        GrowRowsTo(extentRow);
                }

                SyncLabel();
                return;
            }

            cellRef = Anchor(cellRef);
            if (cellRef == SelectedRef && ExtentRef == null)
                return;

            if (IsEditing && !CommitEdit())
                return;

            if (SheetRef.TryParse(cellRef, out _, out var targetRow))
                GrowRowsTo(targetRow);

            _editError = null;
            sheet = CurrentSheet;
            sheet.SelectedRef = cellRef;
            sheet.ExtentRef = null;
            SyncLabel();
        }

        private void SelectRegion(SheetRegion region, bool focus = true, bool grow = true)
        {
            if (IsEditing && !CommitEdit())
                return;

            if (grow)
                GrowRowsTo(region.Bottom);
            var sheet = CurrentSheet;
            sheet.SelectedRef = SheetRef.Make(region.Left, region.Top);
            sheet.ExtentRef = region.IsSingle ? null : SheetRef.Make(region.Right, region.Bottom);
            if (focus)
                _focusSink = true;

            SyncLabel();
        }

        private void EditFromGrid(string cellRef)
        {
            SelectCell(cellRef, true);
            BeginCellEdit(true);
        }

        private void Move(int rowStep, int columnStep)
        {
            if (!SheetRef.TryParse(SelectedRef, out var column, out var row))
            {
                column = 0;
                row = 0;
            }

            var current = CurrentSheet.MergeAt(row, column) ?? new SheetRegion(row, column, row, column);
            var nextRow = rowStep == 0 ? row : Step(rowStep > 0 ? current.Bottom : current.Top, rowStep, true) ?? row;
            var nextColumn = columnStep == 0 ? column : Step(columnStep > 0 ? current.Right : current.Left, columnStep, false) ?? column;
            SelectCell(SheetRef.Make(nextColumn, nextRow), true);
        }

        private void Extend(int rowStep, int columnStep)
        {
            if (!SheetRef.TryParse(ExtentRef ?? SelectedRef, out var column, out var row))
                return;

            var nextRow = rowStep == 0 ? row : Step(row, rowStep, true) ?? row;
            var nextColumn = columnStep == 0 ? column : Step(column, columnStep, false) ?? column;
            SelectCell(SheetRef.Make(nextColumn, nextRow), true, true);
        }

        private int? Step(int from, int step, bool rows)
        {
            if (step == 0)
                return from;

            var limit = rows ? MaxRow : MaxColumn;
            var next = from + step;
            while (next >= 0 && next <= limit && (rows ? CurrentSheet.IsRowHidden(next) : CurrentSheet.IsColumnHidden(next)))
                next += step;

            return next < 0 || next > limit ? null : next;
        }

        private string Anchor(string cellRef)
        {
            if (!SheetRef.TryParse(cellRef, out var column, out var row))
                return cellRef;

            var merge = CurrentSheet.MergeAt(row, column);
            return merge.HasValue ? SheetRef.Make(merge.Value.Left, merge.Value.Top) : cellRef;
        }

        private void Jump(int rowStep, int columnStep, bool extend)
        {
            if (!SheetRef.TryParse(extend ? ExtentRef ?? SelectedRef : SelectedRef, out var column, out var row))
                return;

            var (nextRow, nextColumn) = SheetNavigation.Jump(CurrentSheet, row, column, rowStep, columnStep);
            SelectCell(SheetRef.Make(nextColumn, nextRow), true, extend);
        }

        private static (int Row, int Column) Step(string key) => key switch
        {
            "ArrowUp" => (-1, 0),
            "ArrowDown" => (1, 0),
            "ArrowLeft" => (0, -1),
            "ArrowRight" => (0, 1),
            _ => (0, 0),
        };

        private void SelectAll()
        {
            var sheet = CurrentSheet;
            SheetRef.TryParse(SelectedRef, out var column, out var row);
            var all = new SheetRegion(0, 0, MaxRow, MaxColumn);
            var region = SheetRegion.Around(sheet, column, row);
            SelectRegion(region.HasValue && region.Value != Selection ? region.Value : all, true, false);
        }

        private void HandlePointer(SheetPointer pointer)
        {
            var cellRef = pointer.Row >= 0 && pointer.Column >= 0 ? SheetRef.Make(Math.Min(pointer.Column, MaxColumn), Math.Min(pointer.Row, MaxRow)) : null;
            switch (pointer.Phase)
            {
                case "down":
                    if (cellRef == null)
                        return;

                    if (pointer.Ctrl && TryFollowLink(pointer.Row, pointer.Column))
                        return;

                    if (IsEditing && CanPoint())
                    {
                        StartPoint(pointer.Row, pointer.Column);
                        return;
                    }

                    SelectCell(cellRef, true, pointer.Shift);
                    _dragging = true;
                    break;
                case "move":
                    if (cellRef == null)
                        return;

                    if (_pointing)
                        SetPoint(SheetRegion.Span(_pointRow, _pointColumn, pointer.Row, pointer.Column));
                    else if (_dragging && !IsEditing)
                        SelectCell(cellRef, true, true);
                    break;
                case "up":
                    _pointing = false;
                    if (_dragging && _painter != null)
                        ApplyPainter();

                    _dragging = false;
                    break;
                case "fill-move":
                    if (cellRef != null && !IsLocked)
                    {
                        var target = FillTarget(Selection, pointer.Row, pointer.Column);
                        _fillPreview = target == Selection ? null : target;
                    }
                    break;
                case "fill-up":
                    var preview = _fillPreview;
                    _fillPreview = null;
                    if (preview.HasValue)
                        Fill(Selection, preview.Value, true, preview.Value);
                    break;
                case "column":
                    SelectHeader(SheetRegion.Span(0, pointer.Column, MaxRow, pointer.Column), pointer.Shift);
                    break;
                case "row":
                    SelectHeader(SheetRegion.Span(pointer.Row, 0, pointer.Row, MaxColumn), pointer.Shift);
                    break;
                case "all":
                    SelectRegion(new SheetRegion(0, 0, MaxRow, MaxColumn), true, false);
                    break;
            }
        }

        private void SelectHeader(SheetRegion region, bool extend)
        {
            if (extend)
            {
                var current = Selection;
                region = SheetRegion.Span(
                    Math.Min(region.Top, current.Top),
                    Math.Min(region.Left, current.Left),
                    Math.Max(region.Bottom, current.Bottom),
                    Math.Max(region.Right, current.Right));
            }

            SelectRegion(region, true, false);
        }

        private bool CanPoint()
        {
            var draft = _draft ?? string.Empty;
            if (!draft.TrimStart().StartsWith("=", StringComparison.Ordinal))
                return false;

            if (EndsWithPointedRef(draft))
                return true;

            var trimmed = draft.TrimEnd();
            return trimmed.Length > 0 && PointAfter.IndexOf(trimmed[^1]) >= 0;
        }

        private bool EndsWithPointedRef(string draft) =>
            _pointStart >= 0 && _pointText != null && _pointStart + _pointText.Length == draft.Length && draft.EndsWith(_pointText, StringComparison.Ordinal);

        private void StartPoint(int row, int column)
        {
            var draft = _draft ?? string.Empty;
            if (!EndsWithPointedRef(draft))
                _pointStart = draft.Length;

            _pointing = true;
            _pointRow = row;
            _pointColumn = column;
            SetPoint(SheetRegion.Span(row, column, row, column));
        }

        private void SetPoint(SheetRegion region)
        {
            var draft = _draft ?? string.Empty;
            var text = region.ToRef();
            _draft = draft.Substring(0, Math.Min(_pointStart, draft.Length)) + text;
            _pointText = text;
            _pointRegion = region;
            if (_editInCell)
                _sinkSeed = _draft;
        }

        private SheetRegion ClipToData(SheetRegion region)
        {
            var sheet = CurrentSheet;
            var bottom = Math.Min(region.Bottom, Math.Max(region.Top, sheet.Rows.Count - 1));
            var right = Math.Min(region.Right, Math.Max(region.Left, sheet.Columns.Count - 2));
            return new SheetRegion(region.Top, region.Left, bottom, right);
        }
    }
}
