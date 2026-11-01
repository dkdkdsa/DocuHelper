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
        private void OpenMenu(SheetMenuRequest request)
        {
            if (IsLocked)
                return;

            if (IsEditing && !CommitEdit())
                return;

            var selection = Selection;
            switch (request.Kind)
            {
                case "column":
                    if (!WholeColumns || request.Column < selection.Left || request.Column > selection.Right)
                        SelectHeader(SheetRegion.Span(0, request.Column, MaxRow, request.Column), false);

                    _menuItems = LineMenu(false);
                    break;
                case "row":
                    if (!WholeRows || request.Row < selection.Top || request.Row > selection.Bottom)
                        SelectHeader(SheetRegion.Span(request.Row, 0, request.Row, MaxColumn), false);

                    _menuItems = LineMenu(true);
                    break;
                case "tab":
                    if (request.Row < 0 || request.Row >= Model.Sheets.Count)
                        return;

                    _menuItems = TabMenu(request.Row);
                    break;
                case "validation":
                    if (!selection.Contains(request.Row, request.Column))
                        SelectCell(SheetRef.Make(request.Column, request.Row), true);

                    _menuItems = ValidationItems(request.Row, request.Column);
                    if (_menuItems.Count == 0)
                        return;

                    break;
                default:
                    if (!selection.Contains(request.Row, request.Column))
                        SelectCell(SheetRef.Make(request.Column, request.Row), true);

                    _menuItems = CellMenu();
                    break;
            }

            _menu = request;
        }

        private static SheetMenuItem Separator() => new(string.Empty, string.Empty, Separator: true);

        private IReadOnlyList<SheetMenuItem> ClipboardItems() => new[]
        {
            new SheetMenuItem(SheetCommands.Cut, "잘라내기"),
            new SheetMenuItem(SheetCommands.Copy, "복사"),
            new SheetMenuItem(SheetCommands.Paste, "붙여넣기"),
            Separator(),
        };

        private IReadOnlyList<SheetMenuItem> LineMenu(bool rows)
        {
            var items = ClipboardItems().ToList();
            items.Add(new SheetMenuItem(rows ? SheetCommands.InsertRowsAbove : SheetCommands.InsertColumnsLeft, rows ? "행 넣기" : "열 넣기"));
            items.Add(new SheetMenuItem(rows ? SheetCommands.DeleteRows : SheetCommands.DeleteColumns, rows ? "행 지우기" : "열 지우기"));
            items.Add(new SheetMenuItem(SheetCommands.Clear, "내용 지우기"));
            items.Add(Separator());
            items.Add(new SheetMenuItem(rows ? SheetCommands.FitRows : SheetCommands.FitColumns, rows ? "행 높이 자동 맞춤" : "열 너비 자동 맞춤"));
            items.Add(new SheetMenuItem(rows ? SheetCommands.HideRows : SheetCommands.HideColumns, "숨기기"));
            items.Add(new SheetMenuItem(rows ? SheetCommands.ShowRows : SheetCommands.ShowColumns, "숨기기 취소"));
            return items;
        }

        private IReadOnlyList<SheetMenuItem> CellMenu()
        {
            var items = ClipboardItems().ToList();
            items.Add(new SheetMenuItem(SheetCommands.InsertRowsAbove, "위에 행 넣기"));
            items.Add(new SheetMenuItem(SheetCommands.InsertColumnsLeft, "왼쪽에 열 넣기"));
            items.Add(new SheetMenuItem(SheetCommands.DeleteRows, "행 지우기"));
            items.Add(new SheetMenuItem(SheetCommands.DeleteColumns, "열 지우기"));
            items.Add(new SheetMenuItem(SheetCommands.Clear, "내용 지우기"));
            items.Add(Separator());
            items.Add(new SheetMenuItem(SheetCommands.Merge, IsMergedSelection ? "병합 풀기" : "병합하고 가운데 맞춤", !IsMergedSelection && Selection.IsSingle));
            return items;
        }

        private IReadOnlyList<SheetMenuItem> TabMenu(int index)
        {
            var sheets = Model.Sheets;
            var visible = Enumerable.Range(0, sheets.Count).Where(i => !sheets[i].IsHidden).ToList();
            var position = visible.IndexOf(index);
            var items = new List<SheetMenuItem>
            {
                new(SheetCommands.RenameSheet + ":" + index, "이름 바꾸기"),
                new(SheetCommands.CopySheet + ":" + index, "복사본 만들기"),
                new(SheetCommands.DeleteSheet + ":" + index, "지우기", visible.Count <= 1),
                Separator(),
                new(SheetCommands.MoveSheetLeft + ":" + index, "왼쪽으로 옮기기", position <= 0),
                new(SheetCommands.MoveSheetRight + ":" + index, "오른쪽으로 옮기기", position < 0 || position >= visible.Count - 1),
                Separator(),
                new(SheetCommands.HideSheet + ":" + index, "숨기기", visible.Count <= 1),
            };

            foreach (var hidden in sheets.Where(s => s.IsHidden))
                items.Add(new SheetMenuItem(SheetCommands.ShowSheet + hidden.Name, "‘" + hidden.Name + "’ 다시 보이기"));

            return items;
        }

        private async Task PickMenuAsync(string key)
        {
            var menu = _menu;
            CloseMenu();
            if (menu != null && key.StartsWith(ValidationPickPrefix, StringComparison.Ordinal))
            {
                WriteValidationValue(menu.Row, menu.Column, key.Substring(ValidationPickPrefix.Length));
                return;
            }

            await RunCommandAsync(key);
        }

        private void CloseMenu()
        {
            _menu = null;
            _menuItems = Array.Empty<SheetMenuItem>();
        }

        private void RunSheetCommand(string command)
        {
            if (command.StartsWith(SheetCommands.ShowSheet, StringComparison.Ordinal))
            {
                var name = command.Substring(SheetCommands.ShowSheet.Length);
                if (Edit(EditCommand.Create("change_sheet", new { sheet = name, hidden = false })))
                    ActivateSheet(name);

                return;
            }

            var colon = command.LastIndexOf(':');
            if (colon < 0 || !int.TryParse(command.AsSpan(colon + 1), out var index) || index < 0 || index >= Model.Sheets.Count)
                return;

            var sheet = Model.Sheets[index];
            switch (command.Substring(0, colon))
            {
                case SheetCommands.RenameSheet:
                    _focusSink = false;
                    _tabs?.BeginRename(index);
                    break;
                case SheetCommands.CopySheet:
                    if (Edit(EditCommand.Create("copy_sheet", new { sheet = sheet.Name })))
                        ActivateSheet(index + 1);
                    break;
                case SheetCommands.DeleteSheet:
                    var filled = sheet.Rows.Sum(r => r.Cells.Count(c => SheetRegion.HasValue(c)));
                    if (filled == 0)
                        Edit(EditCommand.Create("delete_sheet", new { sheet = sheet.Name }));
                    else
                        _confirm = new Confirm($"‘{sheet.Name}’ 시트와 그 안의 내용 {filled}칸을 지울까요? 되돌리기로 다시 살릴 수 있어요.", "시트 지우기", () => Edit(EditCommand.Create("delete_sheet", new { sheet = sheet.Name })));
                    break;
                case SheetCommands.MoveSheetLeft:
                case SheetCommands.MoveSheetRight:
                    var step = command.StartsWith(SheetCommands.MoveSheetLeft, StringComparison.Ordinal) ? -1 : 1;
                    var target = index + step;
                    while (target >= 0 && target < Model.Sheets.Count && Model.Sheets[target].IsHidden)
                        target += step;

                    if (target >= 0 && target < Model.Sheets.Count && Edit(EditCommand.Create("change_sheet", new { sheet = sheet.Name, position = target + 1 })))
                        ActivateSheet(sheet.Name);
                    break;
                case SheetCommands.HideSheet:
                    Edit(EditCommand.Create("change_sheet", new { sheet = sheet.Name, hidden = true }));
                    break;
            }
        }

        private void RenameSheet((int Index, string Name) rename)
        {
            if (IsLocked || rename.Index < 0 || rename.Index >= Model.Sheets.Count)
                return;

            var sheet = Model.Sheets[rename.Index];
            var selected = sheet.SelectedRef;
            if (Edit(EditCommand.Create("change_sheet", new { sheet = sheet.Name, new_name = rename.Name })))
            {
                var renamed = Model.Sheets.FirstOrDefault(s => s.Name == rename.Name);
                if (renamed != null)
                    renamed.SelectedRef = selected;

                ActivateSheet(rename.Name);
            }
        }

        private void ActivateSheet(string name)
        {
            var index = Model.Sheets.FindIndex(s => s.Name == name);
            if (index >= 0)
                ActivateSheet(index);
        }

        private void ActivateSheet(int index)
        {
            var model = Model;
            if (index < 0 || index >= model.Sheets.Count || model.Sheets[index].IsHidden)
                return;

            model.ActiveSheetIndex = index;
            _fillPreview = null;
            SyncLabel();
        }

        private void AcceptConfirm()
        {
            var confirm = _confirm;
            _confirm = null;
            _focusSink = true;
            confirm?.Run();
        }

        private void DismissConfirm()
        {
            _confirm = null;
            _focusSink = true;
        }

        private async Task RunJsAsync(string identifier, string argument)
        {
            try
            {
                await JS.InvokeVoidAsync(identifier, argument);
            }
            catch (JSException)
            {
            }
            catch (JSDisconnectedException)
            {
            }
        }

        private sealed record Confirm(string Text, string Button, Action Run);
    }
}
