using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private const string ValidationPickPrefix = "validation:";
        private const string ProtectedMessage = "보호된 시트의 잠긴 칸은 고칠 수 없어요. 시트 보호를 풀어야 고칠 수 있어요.";

        [Inject]
        private IEnumerable<IShellService> Shells { get; set; }

        private RenderFragment<SheetCellContext> CellDecorations => cell => builder =>
        {
            builder.OpenComponent<SheetCellDecorations>(0);
            builder.AddAttribute(1, nameof(SheetCellDecorations.Context), cell);
            builder.AddAttribute(2, nameof(SheetCellDecorations.OnMenu), EventCallback.Factory.Create<SheetMenuRequest>(this, OpenMenu));
            builder.CloseComponent();
        };

        private string CellClassHook(SheetCellContext context)
        {
            var cell = context.Cell;
            if (cell == null)
                return null;

            var classes = new List<string>();
            if (cell.Hyperlink != null)
                classes.Add("lnk");

            if (cell.Note != null)
                classes.Add("ovf");

            if (cell.Visual?.Bold == true || cell.TableLook?.Bold == true)
                classes.Add("b");

            if (cell.Visual?.Italic == true)
                classes.Add("i");

            if (cell.Visual?.Underline == true)
                classes.Add("u");

            return classes.Count == 0 ? null : string.Join(" ", classes);
        }

        private string CellStyleHook(SheetCellContext context)
        {
            var cell = context.Cell;
            if (cell == null)
                return null;

            var visual = cell.Visual;
            var parts = new List<string>();
            if (cell.FillColor == null)
            {
                var fill = visual?.ScaleFill ?? visual?.FillColor ?? cell.TableLook?.Fill;
                if (fill != null)
                    parts.Add("background-color: #" + fill);
            }

            if (cell.FontColor == null)
            {
                var font = visual?.FontColor ?? cell.TableLook?.FontColor;
                if (font != null)
                    parts.Add("color: #" + font);
            }

            if (visual?.Strike == true)
                parts.Add("text-decoration: line-through");

            if (visual?.HasIcon == true)
                parts.Add("padding-left: 20px");

            return parts.Count == 0 ? null : string.Join("; ", parts);
        }

        private IReadOnlyList<SheetMenuItem> ValidationItems(int row, int column)
        {
            var rule = CurrentSheet.FindValidation(row, column);
            if (rule == null || !rule.HasDropDown)
                return Array.Empty<SheetMenuItem>();

            return rule.Items.Select(item => new SheetMenuItem(ValidationPickPrefix + item, item)).ToList();
        }

        private void WriteValidationValue(int row, int column, string value) => ApplyText(CurrentSheet.Name, SheetRef.Make(column, row), value);

        private bool IsProtectedCell(Sheet sheet, int row, int column) => sheet.IsProtected && (sheet.FindCell(row, column)?.Locked ?? true);

        private bool SelectionIsLocked => CurrentSheet.IsProtected && (SelectedCell?.Locked ?? true);

        private bool TryFollowLink(int row, int column)
        {
            var link = CurrentSheet.FindCell(row, column)?.Hyperlink;
            if (link == null)
                return false;

            if (link.IsExternal)
                Shells.FirstOrDefault()?.OpenFile(link.Url);
            else if (link.IsInternal)
                NavigateTo(link.Location);

            return true;
        }

        private void NavigateTo(string location)
        {
            var text = location ?? string.Empty;
            var bang = text.LastIndexOf('!');
            var sheetName = bang < 0 ? CurrentSheet.Name : text.Substring(0, bang).Trim('\'');
            var cellRef = (bang < 0 ? text : text.Substring(bang + 1)).Split(':')[0].Replace("$", string.Empty);
            var index = Model.Sheets.FindIndex(sheet => sheet.Name == sheetName);
            if (index < 0)
            {
                _editError = "링크가 가리키는 시트를 찾을 수 없어요.";
                return;
            }

            if (IsEditing && !CommitEdit())
                return;

            Model.ActiveSheetIndex = index;
            if (SheetRef.TryParse(cellRef, out var column, out var row))
                SelectRegion(new SheetRegion(row, column, row, column));
        }
    }
}
