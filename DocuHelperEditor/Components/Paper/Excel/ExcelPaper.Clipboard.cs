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
        [Inject]
        private IClipboardService Clipboards { get; set; }

        private async Task CopyAsync(bool cut)
        {
            if (IsEditing)
                return;

            var sheet = CurrentSheet;
            var region = ClipToData(Selection);
            var texts = new List<IReadOnlyList<string>>();
            var edits = new string[region.RowCount][];
            for (var r = 0; r < region.RowCount; r++)
            {
                var shown = new string[region.ColumnCount];
                edits[r] = new string[region.ColumnCount];
                for (var c = 0; c < region.ColumnCount; c++)
                {
                    var cell = sheet.FindCell(region.Top + r, region.Left + c);
                    shown[c] = cell == null || cell.IsPending ? string.Empty : cell.Text ?? string.Empty;
                    edits[r][c] = cell?.EditText ?? string.Empty;
                }

                texts.Add(shown);
            }

            var text = SheetTsv.Format(texts);
            try
            {
                await Clipboards.SetTextAsync(text);
            }
            catch (Exception e)
            {
                AppLog.Write("Copy to clipboard failed", e);
                _editError = "복사하지 못했어요. 다시 해 주세요.";
                return;
            }

            _clip = new Clip(sheet.Name, region, edits, text, cut && !IsLocked);
            _editError = null;
        }

        private void HandlePaste(string text)
        {
            if (IsLocked || IsEditing || string.IsNullOrEmpty(text))
                return;

            var sheet = CurrentSheet;
            var target = Selection;
            var clip = _clip != null && SheetTsv.Normalize(_clip.Text) == SheetTsv.Normalize(text) ? _clip : null;
            if (clip != null && PasteWithTool())
                return;

            var source = clip != null ? clip.Edits : SheetTsv.Parse(text).Select(r => r.ToArray()).ToArray();
            if (source.Length == 0)
                return;

            var rows = source.Length;
            var columns = source.Max(r => r.Length);
            var tile = rows == 1 && columns == 1 && !target.IsSingle;
            var area = tile ? target : new SheetRegion(target.Top, target.Left, target.Top + rows - 1, target.Left + columns - 1);
            var values = new string[area.RowCount][];
            for (var r = 0; r < area.RowCount; r++)
            {
                values[r] = new string[area.ColumnCount];
                for (var c = 0; c < area.ColumnCount; c++)
                {
                    var sourceRow = tile ? 0 : r;
                    var sourceColumn = tile ? 0 : c;
                    var value = sourceColumn < source[sourceRow].Length ? source[sourceRow][sourceColumn] ?? string.Empty : string.Empty;
                    if (clip != null && !clip.IsCut && value.StartsWith("=", StringComparison.Ordinal))
                    {
                        var rowDelta = area.Top + r - (clip.Region.Top + sourceRow);
                        var columnDelta = area.Left + c - (clip.Region.Left + sourceColumn);
                        value = "=" + FormulaText.Shift(FormulaText.Strip(value), rowDelta, columnDelta);
                    }

                    values[r][c] = value;
                }
            }

            var commands = new List<EditCommand>();
            if (clip != null && clip.IsCut)
            {
                var sameSheet = clip.Sheet == sheet.Name;
                var clear = new string[clip.Region.RowCount][];
                for (var r = 0; r < clip.Region.RowCount; r++)
                {
                    clear[r] = new string[clip.Region.ColumnCount];
                    for (var c = 0; c < clip.Region.ColumnCount; c++)
                        clear[r][c] = sameSheet && area.Contains(clip.Region.Top + r, clip.Region.Left + c) ? null : string.Empty;
                }

                commands.Add(EditCommand.Create("write_cells", new { sheet = clip.Sheet, start = SheetRef.Make(clip.Region.Left, clip.Region.Top), values = clear }));
            }

            commands.Add(EditCommand.Create("write_cells", new { sheet = sheet.Name, start = SheetRef.Make(area.Left, area.Top), values }));
            if (!Edits(commands))
                return;

            if (clip != null && clip.IsCut)
                _clip = null;

            SelectRegion(area);
        }

        private static SheetRegion FillTarget(SheetRegion source, int row, int column)
        {
            var down = row - source.Bottom;
            var up = source.Top - row;
            var right = column - source.Right;
            var left = source.Left - column;
            var vertical = Math.Max(down, up);
            var horizontal = Math.Max(right, left);
            if (vertical <= 0 && horizontal <= 0)
                return source;

            if (vertical >= horizontal)
                return down > 0 ? new SheetRegion(source.Top, source.Left, row, source.Right) : new SheetRegion(row, source.Left, source.Bottom, source.Right);

            return right > 0 ? new SheetRegion(source.Top, source.Left, source.Bottom, column) : new SheetRegion(source.Top, column, source.Bottom, source.Right);
        }

        private void FillAlong(bool down)
        {
            if (IsLocked)
                return;

            var selection = Selection;
            if (down)
            {
                if (selection.RowCount > 1)
                    Fill(new SheetRegion(selection.Top, selection.Left, selection.Top, selection.Right), selection, false, selection);
                else if (selection.Top > 0)
                    Fill(new SheetRegion(selection.Top - 1, selection.Left, selection.Top - 1, selection.Right), new SheetRegion(selection.Top - 1, selection.Left, selection.Bottom, selection.Right), false, selection);

                return;
            }

            if (selection.ColumnCount > 1)
                Fill(new SheetRegion(selection.Top, selection.Left, selection.Bottom, selection.Left), selection, false, selection);
            else if (selection.Left > 0)
                Fill(new SheetRegion(selection.Top, selection.Left - 1, selection.Bottom, selection.Left - 1), new SheetRegion(selection.Top, selection.Left - 1, selection.Bottom, selection.Right), false, selection);
        }

        private void Fill(SheetRegion source, SheetRegion target, bool series, SheetRegion selectAfter)
        {
            if (IsLocked)
                return;

            if (IsEditing && !CommitEdit())
                return;

            SheetRegion extension;
            bool backward;
            bool vertical;
            if (target.Bottom > source.Bottom)
            {
                extension = new SheetRegion(source.Bottom + 1, source.Left, target.Bottom, source.Right);
                backward = false;
                vertical = true;
            }
            else if (target.Top < source.Top)
            {
                extension = new SheetRegion(target.Top, source.Left, source.Top - 1, source.Right);
                backward = true;
                vertical = true;
            }
            else if (target.Right > source.Right)
            {
                extension = new SheetRegion(source.Top, source.Right + 1, source.Bottom, target.Right);
                backward = false;
                vertical = false;
            }
            else if (target.Left < source.Left)
            {
                extension = new SheetRegion(source.Top, target.Left, source.Bottom, source.Left - 1);
                backward = true;
                vertical = false;
            }
            else
            {
                return;
            }

            var sheet = CurrentSheet;
            var values = Enumerable.Range(0, extension.RowCount).Select(_ => new string[extension.ColumnCount]).ToArray();
            var lines = vertical ? source.ColumnCount : source.RowCount;
            var count = vertical ? extension.RowCount : extension.ColumnCount;
            for (var line = 0; line < lines; line++)
            {
                var cells = vertical
                    ? Enumerable.Range(source.Top, source.RowCount).Select(r => sheet.FindCell(r, source.Left + line)).ToList()
                    : Enumerable.Range(source.Left, source.ColumnCount).Select(c => sheet.FindCell(source.Top + line, c)).ToList();
                var produced = SheetFill.Extend(cells, count, backward, vertical, series);
                for (var k = 0; k < count; k++)
                {
                    var position = backward ? count - 1 - k : k;
                    if (vertical)
                        values[position][line] = produced[k];
                    else
                        values[line][position] = produced[k];
                }
            }

            if (Edit(EditCommand.Create("write_cells", new { sheet = sheet.Name, start = SheetRef.Make(extension.Left, extension.Top), values })))
                SelectRegion(selectAfter);
        }

        private async Task PasteFromClipboardAsync()
        {
            string text;
            try
            {
                text = await Clipboards.GetTextAsync();
            }
            catch (Exception e)
            {
                AppLog.Write("Paste from clipboard failed", e);
                _editError = "붙여넣지 못했어요. Ctrl+V로 다시 해 주세요.";
                return;
            }

            HandlePaste(text);
        }

        private sealed record Clip(string Sheet, SheetRegion Region, string[][] Edits, string Text, bool IsCut);
    }
}
