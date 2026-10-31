using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocuHelperEditor.State.Ribbon.Excel;
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
        private async Task HandleGridKeyAsync(KeyboardEventArgs e)
        {
            if (e.IsComposing)
                return;

            if (_editInCell)
            {
                switch (e.Key)
                {
                    case "Enter":
                        if (CommitEdit())
                            Move(e.ShiftKey ? -1 : 1, 0);
                        else
                            _focusSink = true;
                        break;
                    case "Tab":
                        if (CommitEdit())
                            Move(0, e.ShiftKey ? -1 : 1);
                        else
                            _focusSink = true;
                        break;
                    case "Escape":
                        CancelEdit();
                        _editError = null;
                        _focusSink = true;
                        break;
                    case "ArrowUp":
                    case "ArrowDown":
                    case "ArrowLeft":
                    case "ArrowRight":
                        if (_editTyped)
                        {
                            if (CommitEdit())
                            {
                                var (rowStep, columnStep) = Step(e.Key);
                                Move(rowStep, columnStep);
                            }
                            else
                            {
                                _focusSink = true;
                            }
                        }
                        break;
                }

                return;
            }

            if (ExcelCommandRouter.TryShortcut(e.Code, e.CtrlKey || e.MetaKey, e.ShiftKey, e.AltKey, out var shortcut, out var shortcutArg))
            {
                await _router.InvokeAsync(shortcut, shortcutArg);
                return;
            }

            var arrow = Step(e.Key);
            if ((e.CtrlKey || e.MetaKey) && !e.AltKey)
            {
                if (arrow != (0, 0))
                {
                    Jump(arrow.Row, arrow.Column, e.ShiftKey);
                    return;
                }

                switch (e.Code)
                {
                    case "KeyZ":
                        if (e.ShiftKey)
                            await Workspace.RedoAsync();
                        else
                            await Workspace.UndoAsync();
                        break;
                    case "KeyY":
                        await Workspace.RedoAsync();
                        break;
                    case "KeyA":
                        SelectAll();
                        break;
                    case "KeyC":
                        await CopyAsync(false);
                        break;
                    case "KeyX":
                        await CopyAsync(true);
                        break;
                    case "KeyD":
                        FillAlong(true);
                        break;
                    case "KeyR":
                        FillAlong(false);
                        break;
                    case "KeyF":
                        OpenFind(false);
                        break;
                    case "KeyH":
                        OpenFind(true);
                        break;
                    case "Digit9":
                        await RunCommandAsync(e.ShiftKey ? SheetCommands.ShowRows : SheetCommands.HideRows);
                        break;
                    case "Digit0":
                        await RunCommandAsync(e.ShiftKey ? SheetCommands.ShowColumns : SheetCommands.HideColumns);
                        break;
                    case "Minus":
                    case "NumpadSubtract":
                        if (WholeRows || WholeColumns)
                            await RunCommandAsync(WholeRows ? SheetCommands.DeleteRows : SheetCommands.DeleteColumns);
                        break;
                    case "Equal" when e.ShiftKey:
                    case "NumpadAdd":
                        if (WholeRows || WholeColumns)
                            await RunCommandAsync(WholeRows ? SheetCommands.InsertRowsAbove : SheetCommands.InsertColumnsLeft);
                        break;
                }

                return;
            }

            if (arrow != (0, 0))
            {
                if (e.ShiftKey)
                    Extend(arrow.Row, arrow.Column);
                else
                    Move(arrow.Row, arrow.Column);

                return;
            }

            switch (e.Key)
            {
                case "Tab":
                    Move(0, e.ShiftKey ? -1 : 1);
                    break;
                case "Enter":
                case "F2":
                    BeginCellEdit(true);
                    break;
                case "Backspace":
                    BeginCellEdit(false);
                    break;
                case "Delete":
                    ClearSelection();
                    break;
                case "Escape":
                    _clip = null;
                    _painter = null;
                    _fillPreview = null;
                    break;
            }
        }

        private void BeginCellEdit(bool keepText)
        {
            if (IsLocked)
                return;

            if (IsEditing && !CommitEdit())
                return;

            var text = keepText ? (SelectedCell?.EditText ?? string.Empty) : string.Empty;
            _editSheet = CurrentSheet.Name;
            _editRef = SelectedRef;
            _editInCell = true;
            _editInBar = false;
            _editTyped = false;
            _draft = text;
            _sinkSeed = text;
            _focusSink = true;
        }

        private void HandleSinkInput(string value)
        {
            if (IsLocked || _editInBar)
                return;

            if (!_editInCell)
            {
                _editSheet = CurrentSheet.Name;
                _editRef = SelectedRef;
                _editInCell = true;
                _editTyped = true;
                _sinkSeed = string.Empty;
            }

            _draft = value ?? string.Empty;
        }

        private void HandleSinkBlur()
        {
            if (_editInCell && !_pointing)
                CommitEdit();
        }

        private void HandleBarEdit(string value)
        {
            if (IsLocked || _editInCell)
                return;

            if (!_editInBar)
            {
                _editSheet = CurrentSheet.Name;
                _editRef = SelectedRef;
                _editInBar = true;
                _editTyped = false;
            }

            _draft = value ?? string.Empty;
        }

        private void HandleBarCommit()
        {
            if (_editInBar && !CommitEdit())
                return;

            Move(1, 0);
            _focusSink = true;
        }

        private void HandleBarCancel()
        {
            if (_editInBar)
                CancelEdit();

            _focusSink = true;
        }

        private void HandleBarLeave()
        {
            if (_editInBar && !_pointing)
                CommitEdit();
        }

        private void GoTo(string text)
        {
            var parts = (text ?? string.Empty).Split(':');
            if (!SheetRef.TryParse(parts[0].Trim(), out var column, out var row))
                return;

            if (column > MaxColumn || row > MaxRow)
                return;

            if (parts.Length == 2 && SheetRef.TryParse(parts[1].Trim(), out var endColumn, out var endRow))
            {
                SelectRegion(SheetRegion.Span(row, column, Math.Min(endRow, MaxRow), Math.Min(endColumn, MaxColumn)));
                return;
            }

            SelectCell(SheetRef.Make(column, row), true);
        }

        private bool CommitEdit()
        {
            if (!IsEditing)
                return true;

            var sheet = _editSheet;
            var cellRef = _editRef;
            var value = _draft ?? string.Empty;
            var inCell = _editInCell;
            ResetEdit();
            _clip = null;
            ApplyText(sheet, cellRef, value);
            if (_editError == null)
                return true;

            if (sheet == CurrentSheet.Name && cellRef == SelectedRef)
            {
                _editSheet = sheet;
                _editRef = cellRef;
                _editInCell = inCell;
                _editInBar = !inCell;
                _draft = value;
                _sinkSeed = value;
            }

            return false;
        }

        private void CancelEdit()
        {
            if (IsEditing)
                ResetEdit();
        }

        private void ResetEdit()
        {
            _editSheet = null;
            _editRef = null;
            _editInCell = false;
            _editInBar = false;
            _editTyped = false;
            _draft = string.Empty;
            _sinkSeed = string.Empty;
            _pointing = false;
            _pointStart = -1;
            _pointText = null;
            _pointRegion = null;
            _editVersion++;
        }

        private void ClearSelection()
        {
            if (IsLocked)
                return;

            var region = ClipToData(Selection);
            if (region.IsSingle)
            {
                ApplyText(CurrentSheet.Name, SheetRef.Make(region.Left, region.Top), string.Empty);
                return;
            }

            var values = Enumerable.Range(0, region.RowCount).Select(_ => Enumerable.Repeat(string.Empty, region.ColumnCount).ToArray()).ToArray();
            Edit(EditCommand.Create("write_cells", new { sheet = CurrentSheet.Name, start = SheetRef.Make(region.Left, region.Top), values }));
        }

        private void ApplyText(string sheetName, string cellRef, string value)
        {
            if (string.IsNullOrEmpty(sheetName) || IsLocked || !SheetRef.TryParse(cellRef, out var column, out var row))
                return;

            var sheet = Model.Sheets.FirstOrDefault(s => s.Name == sheetName);
            var before = sheet?.FindCell(row, column)?.EditText ?? string.Empty;
            if (value == before)
                return;

            Edit(EditCommand.Create("write_cells", new { sheet = sheetName, start = cellRef, values = new[] { new[] { value } } }));
        }
    }
}
