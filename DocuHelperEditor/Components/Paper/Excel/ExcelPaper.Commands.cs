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
        private void SelectSheet(int index)
        {
            if (index < 0 || index >= Model.Sheets.Count || Model.Sheets[index].IsHidden)
                return;

            if (IsEditing && !CommitEdit())
                return;

            var model = Model;
            model.ActiveSheetIndex = Math.Clamp(index, 0, model.Sheets.Count - 1);
            _editError = null;
            _fillPreview = null;
            if (_findOpen)
                Refind();

            SyncLabel();
        }

        private void AddSheet()
        {
            if (IsLocked)
                return;

            if (IsEditing && !CommitEdit())
                return;

            var name = NextSheetName();
            if (!Edit(EditCommand.Create("add_sheet", new { name })))
                return;

            var model = Model;
            var index = model.Sheets.FindIndex(s => s.Name == name);
            if (index >= 0)
                model.ActiveSheetIndex = index;

            SyncLabel();
        }

        private string NextSheetName()
        {
            var names = Model.Sheets.Select(s => s.Name).ToList();
            var number = names.Count + 1;
            while (names.Any(n => string.Equals(n, SheetNamePrefix + number, StringComparison.OrdinalIgnoreCase)))
                number++;

            return SheetNamePrefix + number;
        }

        private bool Edit(EditCommand command)
        {
            var result = Workspace.ApplyEdit(File, command);
            _editError = result.IsError ? result.Content : null;
            return !result.IsError;
        }

        private bool Edits(IReadOnlyList<EditCommand> commands)
        {
            var result = Workspace.ApplyEdits(File, commands);
            _editError = result.IsError ? result.Content : null;
            return !result.IsError;
        }

        private void SortTable(bool descending)
        {
            if (IsLocked)
                return;

            if (IsEditing && !CommitEdit())
                return;

            _focusSink = true;
            var sheet = CurrentSheet;
            if (!SheetRef.TryParse(SelectedRef, out var column, out var row))
                return;

            var selection = Selection;
            var region = selection.RowCount > 1 ? ClipToData(selection) : SheetRegion.Around(sheet, column, row);
            if (region == null)
            {
                _editError = "정렬할 표를 찾지 못했어요. 표 안의 칸을 하나 고른 뒤 눌러 주세요.";
                return;
            }

            if (sheet.IsClipped && region.Value.TouchesEdge(sheet))
            {
                _editError = "표가 너무 커서 여기서는 정렬할 수 없어요. 도우미에게 정렬해 달라고 부탁해 주세요.";
                return;
            }

            var (top, left, bottom, right) = region.Value;
            if (SheetRegion.StartsWithHeader(sheet, top, left, right, column))
                top++;

            while (bottom > top && sheet.FindRow(bottom)?.Kind == SheetRowKind.Total)
                bottom--;

            if (bottom <= top)
            {
                _editError = "정렬하려면 표에 두 줄 이상이 있어야 해요.";
                return;
            }

            var range = SheetRegion.Span(top, left, bottom, right).ToRef();
            Edit(EditCommand.Create("sort_range", new { sheet = sheet.Name, range, by = SheetRef.ColumnName(column), descending }));
        }

        private void AutoSum(string function = "SUM")
        {
            if (IsLocked)
                return;

            if (IsEditing && !CommitEdit())
                return;

            _focusSink = true;
            var sheet = CurrentSheet;
            var selection = Selection;
            if (!selection.IsSingle)
            {
                AutoSumRange(sheet, selection, function);
                return;
            }

            var plan = SheetRegion.PlanSum(sheet, selection.Left, selection.Top);
            if (plan.Problem != null)
            {
                _editError = plan.Problem;
                return;
            }

            var target = SheetRef.Make(plan.Column, plan.Row);
            ApplyText(sheet.Name, target, "=" + function + "(" + plan.Range + ")");
            if (_editError == null)
                SelectCell(target, true);
        }

        private void AutoSumRange(Sheet sheet, SheetRegion selection, string function)
        {
            bool IsNumber(int r, int c) => sheet.FindCell(r, c) is { IsNumber: true } cell && SheetRegion.HasValue(cell);

            if (selection.RowCount > 1)
            {
                var sums = new string[selection.ColumnCount];
                for (var c = selection.Left; c <= selection.Right; c++)
                {
                    if (!Enumerable.Range(selection.Top, selection.RowCount).Any(r => IsNumber(r, c)))
                        continue;

                    if (SheetRegion.HasValue(sheet.FindCell(selection.Bottom + 1, c)))
                    {
                        _editError = "합계를 넣을 아래 칸이 비어 있지 않아요.";
                        return;
                    }

                    sums[c - selection.Left] = "=" + function + "(" + SheetRegion.Span(selection.Top, c, selection.Bottom, c).ToRef() + ")";
                }

                if (sums.All(s => s == null))
                {
                    _editError = "고른 칸에 더할 숫자가 없어요.";
                    return;
                }

                Edit(EditCommand.Create("write_cells", new { sheet = sheet.Name, start = SheetRef.Make(selection.Left, selection.Bottom + 1), values = new[] { sums } }));
                return;
            }

            if (!Enumerable.Range(selection.Left, selection.ColumnCount).Any(c => IsNumber(selection.Top, c)))
            {
                _editError = "고른 칸에 더할 숫자가 없어요.";
                return;
            }

            if (SheetRegion.HasValue(sheet.FindCell(selection.Top, selection.Right + 1)))
            {
                _editError = "합계를 넣을 오른쪽 칸이 비어 있지 않아요.";
                return;
            }

            ApplyText(sheet.Name, SheetRef.Make(selection.Right + 1, selection.Top), "=" + function + "(" + selection.ToRef() + ")");
        }

        private async Task RunCommandAsync(string command)
        {
            if (IsLocked || string.IsNullOrEmpty(command))
                return;

            if (IsEditing && !CommitEdit())
                return;

            _focusSink = true;
            _confirm = null;
            var selection = Selection;
            var rowCount = WholeColumns ? 1 : selection.RowCount;
            var columnCount = WholeRows ? 1 : selection.ColumnCount;
            switch (command)
            {
                case SheetCommands.InsertRowsAbove:
                    ChangeLines("insert_rows_columns", true, selection.Top, selection.Top + rowCount - 1);
                    break;
                case SheetCommands.InsertRowsBelow:
                    ChangeLines("insert_rows_columns", true, selection.Bottom + 1, selection.Bottom + rowCount);
                    break;
                case SheetCommands.InsertColumnsLeft:
                    ChangeLines("insert_rows_columns", false, selection.Left, selection.Left + columnCount - 1);
                    break;
                case SheetCommands.InsertColumnsRight:
                    ChangeLines("insert_rows_columns", false, selection.Right + 1, selection.Right + columnCount);
                    break;
                case SheetCommands.DeleteRows:
                    ChangeLines("delete_rows_columns", true, selection.Top, selection.Bottom);
                    break;
                case SheetCommands.DeleteColumns:
                    ChangeLines("delete_rows_columns", false, selection.Left, selection.Right);
                    break;
                case SheetCommands.HideRows:
                    HideLines(true, selection.Top, selection.Bottom);
                    break;
                case SheetCommands.HideColumns:
                    HideLines(false, selection.Left, selection.Right);
                    break;
                case SheetCommands.ShowRows:
                    ShowLines(true);
                    break;
                case SheetCommands.ShowColumns:
                    ShowLines(false);
                    break;
                case SheetCommands.FitColumns:
                    Edit(EditCommand.Create("format_rows_columns", new { sheet = CurrentSheet.Name, columns = LineRef(false, selection.Left, selection.Right), width = "auto" }));
                    break;
                case SheetCommands.FitRows:
                    Edit(EditCommand.Create("format_rows_columns", new { sheet = CurrentSheet.Name, rows = LineRef(true, selection.Top, selection.Bottom), height = "auto" }));
                    break;
                case SheetCommands.FreezeHere:
                    if (SelectedRef == "A1")
                        _editError = "틀을 고정할 칸을 먼저 골라 주세요. 예를 들어 B2를 고르면 1행과 A열이 늘 보여요.";
                    else
                        Edit(EditCommand.Create("freeze_panes", new { sheet = CurrentSheet.Name, cell = SelectedRef }));
                    break;
                case SheetCommands.FreezeRow:
                    Edit(EditCommand.Create("freeze_panes", new { sheet = CurrentSheet.Name, cell = "A2" }));
                    break;
                case SheetCommands.FreezeColumn:
                    Edit(EditCommand.Create("freeze_panes", new { sheet = CurrentSheet.Name, cell = "B1" }));
                    break;
                case SheetCommands.Unfreeze:
                    Edit(EditCommand.Create("freeze_panes", new { sheet = CurrentSheet.Name, cell = "none" }));
                    break;
                case SheetCommands.Merge:
                    ToggleMerge();
                    break;
                case SheetCommands.Cut:
                    await CopyAsync(true);
                    break;
                case SheetCommands.Copy:
                    await CopyAsync(false);
                    break;
                case SheetCommands.Paste:
                    await PasteFromClipboardAsync();
                    break;
                case SheetCommands.Clear:
                    ClearSelection();
                    break;
                default:
                    RunSheetCommand(command);
                    break;
            }
        }

        private static string LineRef(bool rows, int first, int last) => rows
            ? (first + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + (last + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : SheetRef.ColumnName(first) + ":" + SheetRef.ColumnName(last);

        private void ChangeLines(string tool, bool rows, int first, int last)
        {
            var arguments = rows
                ? (object)new { sheet = CurrentSheet.Name, rows = LineRef(true, first, last) }
                : new { sheet = CurrentSheet.Name, columns = LineRef(false, first, last) };
            if (Edit(EditCommand.Create(tool, arguments)))
                _clip = null;
        }

        private void HideLines(bool rows, int first, int last)
        {
            var arguments = rows
                ? (object)new { sheet = CurrentSheet.Name, rows = LineRef(true, first, last), hidden = true }
                : new { sheet = CurrentSheet.Name, columns = LineRef(false, first, last), hidden = true };
            if (!Edit(EditCommand.Create("format_rows_columns", arguments)))
                return;

            var sheet = CurrentSheet;
            var next = rows ? Step(last, 1, true) ?? Step(first, -1, true) : Step(last, 1, false) ?? Step(first, -1, false);
            if (!next.HasValue)
                return;

            SheetRef.TryParse(SelectedRef, out var column, out var row);
            sheet.SelectedRef = Anchor(rows ? SheetRef.Make(column, next.Value) : SheetRef.Make(next.Value, row));
            sheet.ExtentRef = null;
            SyncLabel();
        }

        private void ShowLines(bool rows)
        {
            var sheet = CurrentSheet;
            var selection = Selection;
            bool Hidden(int index) => rows ? sheet.IsRowHidden(index) : sheet.IsColumnHidden(index);
            var first = rows ? selection.Top : selection.Left;
            var last = rows ? selection.Bottom : selection.Right;
            var hidden = Enumerable.Range(first, last - first + 1).Where(Hidden).ToList();
            if (hidden.Count == 0)
            {
                for (var index = first - 1; index >= 0 && Hidden(index); index--)
                    hidden.Add(index);

                var limit = rows ? sheet.Rows.Count : sheet.Columns.Count;
                for (var index = last + 1; index < limit && Hidden(index); index++)
                    hidden.Add(index);
            }

            if (hidden.Count == 0)
            {
                _editError = rows ? "숨긴 행이 없어요. 숨긴 행의 위아래 행을 함께 고른 뒤 눌러 주세요." : "숨긴 열이 없어요. 숨긴 열의 양옆 열을 함께 고른 뒤 눌러 주세요.";
                return;
            }

            var arguments = rows
                ? (object)new { sheet = sheet.Name, rows = LineRef(true, hidden.Min(), hidden.Max()), hidden = false }
                : new { sheet = sheet.Name, columns = LineRef(false, hidden.Min(), hidden.Max()), hidden = false };
            Edit(EditCommand.Create("format_rows_columns", arguments));
        }

        private void ToggleMerge()
        {
            var sheet = CurrentSheet;
            var selection = Selection;
            if (IsMergedSelection)
            {
                Edit(EditCommand.Create("merge_cells", new { sheet = sheet.Name, range = selection.ToRef(), unmerge = true }));
                return;
            }

            if (selection.IsSingle)
            {
                _editError = "합칠 칸을 두 개 이상 골라 주세요.";
                return;
            }

            var filled = 0;
            for (var r = selection.Top; r <= selection.Bottom; r++)
            {
                for (var c = selection.Left; c <= selection.Right; c++)
                {
                    if (SheetRegion.HasValue(sheet.FindCell(r, c)))
                        filled++;
                }
            }

            if (filled > 1)
            {
                _confirm = new Confirm("합치면 맨 처음 값 하나만 남고 다른 칸의 값은 지워져요.", "그래도 합치기", () => Merge(selection, true));
                return;
            }

            Merge(selection, false);
        }

        private void Merge(SheetRegion region, bool discard)
        {
            if (Edit(EditCommand.Create("merge_cells", new { sheet = CurrentSheet.Name, range = region.ToRef(), center = true, discard_other_values = discard })))
                SelectRegion(region);
        }

        private void HandleResize(SheetResize resize)
        {
            if (IsLocked)
                return;

            if (IsEditing && !CommitEdit())
                return;

            var selection = Selection;
            var first = resize.Index;
            var last = resize.Index;
            if (resize.Rows && WholeRows && resize.Index >= selection.Top && resize.Index <= selection.Bottom)
                (first, last) = (selection.Top, selection.Bottom);
            else if (!resize.Rows && WholeColumns && resize.Index >= selection.Left && resize.Index <= selection.Right)
                (first, last) = (selection.Left, selection.Right);

            object arguments;
            if (resize.Rows)
            {
                object height = resize.Pixels < 0 ? "auto" : Math.Round(resize.Pixels * 11.0 / 20, 2);
                arguments = new { sheet = CurrentSheet.Name, rows = LineRef(true, first, last), height };
            }
            else
            {
                object width = resize.Pixels < 0 ? "auto" : Math.Round(Math.Max(0, resize.Pixels - 5) / 7.0, 2);
                arguments = new { sheet = CurrentSheet.Name, columns = LineRef(false, first, last), width };
            }

            Edit(EditCommand.Create("format_rows_columns", arguments));
        }
    }
}
