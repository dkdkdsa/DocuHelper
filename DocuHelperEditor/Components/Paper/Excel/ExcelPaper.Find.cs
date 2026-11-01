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
        private void OpenFind(bool replace)
        {
            if (IsEditing && !CommitEdit())
                return;

            _findOpen = true;
            _findReplace = replace;
            _findNote = null;
            _findFocus++;
            Refind();
        }

        private void CloseFind()
        {
            _findOpen = false;
            _findNote = null;
            _matches.Clear();
            _matchIndex = -1;
            _focusSink = true;
        }

        private void ToggleReplace()
        {
            _findReplace = !_findReplace;
            _findNote = null;
        }

        private void SetReplacement(string value) => _replaceWith = value ?? string.Empty;

        private void SetFindQuery(string query)
        {
            _findQuery = query ?? string.Empty;
            _findNote = null;
            Refind();
            if (_matches.Count > 0)
                FindStep(1, true);
        }

        private void Refind()
        {
            _matches.Clear();
            _matchIndex = -1;
            if (string.IsNullOrEmpty(_findQuery))
                return;

            var sheet = CurrentSheet;
            foreach (var (r, row) in sheet.Rows.Entries)
            {
                var cells = row.Cells;
                for (var c = 0; c < cells.Count; c++)
                {
                    var text = cells[c] is { IsPending: false } cell ? cell.Text : null;
                    if (!string.IsNullOrEmpty(text) && text.Contains(_findQuery, StringComparison.CurrentCultureIgnoreCase))
                        _matches.Add((r, c));
                }
            }
        }

        private void FindStep(int step) => FindStep(step, false);

        private void FindStep(int step, bool includeCurrent)
        {
            if (_matches.Count == 0)
                return;

            SheetRef.TryParse(SelectedRef, out var column, out var row);
            var current = _matches.FindIndex(m => m.Row == row && m.Column == column);
            if (current >= 0)
            {
                _matchIndex = includeCurrent ? current : (current + step + _matches.Count) % _matches.Count;
            }
            else if (step > 0)
            {
                var next = _matches.FindIndex(m => m.Row > row || (m.Row == row && m.Column > column));
                _matchIndex = next < 0 ? 0 : next;
            }
            else
            {
                var previous = _matches.FindLastIndex(m => m.Row < row || (m.Row == row && m.Column < column));
                _matchIndex = previous < 0 ? _matches.Count - 1 : previous;
            }

            ShowMatch();
        }

        private void ShowMatch()
        {
            if (_matchIndex < 0 || _matchIndex >= _matches.Count)
                return;

            if (IsEditing && !CommitEdit())
                return;

            var (row, column) = _matches[_matchIndex];
            var sheet = CurrentSheet;
            sheet.SelectedRef = SheetRef.Make(column, row);
            sheet.ExtentRef = null;
            _scrollTarget = "#" + SinkId;
            SyncLabel();
        }

        private string ReplaceText(string text) =>
            Regex.Replace(text ?? string.Empty, Regex.Escape(_findQuery), _replaceWith.Replace("$", "$$"), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private void ReplaceOne()
        {
            if (IsLocked || _matches.Count == 0)
                return;

            SheetRef.TryParse(SelectedRef, out var column, out var row);
            var current = _matches.FindIndex(m => m.Row == row && m.Column == column);
            if (current < 0)
            {
                FindStep(1);
                return;
            }

            var cell = CurrentSheet.FindCell(row, column);
            if (!string.IsNullOrEmpty(cell?.Formula))
            {
                _findNote = "수식이 든 칸은 바꾸지 않아요. 다음 칸으로 갈게요.";
                FindStep(1);
                return;
            }

            var replaced = ReplaceText(cell?.EditText);
            if (replaced == (cell?.EditText ?? string.Empty))
            {
                _findNote = "이 칸은 보이는 모양과 실제 값이 달라서 바꾸지 못했어요. 다음 칸으로 갈게요.";
                FindStep(1);
                return;
            }

            var sheetName = CurrentSheet.Name;
            ApplyText(sheetName, SelectedRef, replaced);
            _findNote = null;
            Refind();
            if (_matches.Count > 0)
                FindStep(1);
        }

        private void ReplaceAll()
        {
            if (IsLocked || _matches.Count == 0)
                return;

            var sheet = CurrentSheet;
            var changes = new List<(int Row, int Column, string Text)>();
            var skipped = 0;
            foreach (var (row, column) in _matches)
            {
                var cell = sheet.FindCell(row, column);
                if (cell == null || !string.IsNullOrEmpty(cell.Formula))
                {
                    skipped++;
                    continue;
                }

                var replaced = ReplaceText(cell.EditText);
                if (replaced == cell.EditText)
                {
                    skipped++;
                    continue;
                }

                changes.Add((row, column, replaced));
            }

            if (changes.Count == 0)
            {
                _findNote = "바꿀 수 있는 칸이 없어요. 수식이 들었거나 보이는 모양과 실제 값이 다른 칸이에요.";
                return;
            }

            var area = SheetRegion.Span(changes.Min(c => c.Row), changes.Min(c => c.Column), changes.Max(c => c.Row), changes.Max(c => c.Column));
            var values = Enumerable.Range(0, area.RowCount).Select(_ => new string[area.ColumnCount]).ToArray();
            foreach (var change in changes)
                values[change.Row - area.Top][change.Column - area.Left] = change.Text;

            if (!Edit(EditCommand.Create("write_cells", new { sheet = sheet.Name, start = SheetRef.Make(area.Left, area.Top), values })))
            {
                _findNote = _editError;
                return;
            }

            _findNote = $"{changes.Count}곳을 바꿨어요." + (skipped > 0 ? $" 수식이 들었거나 값이 다른 {skipped}곳은 그대로 뒀어요." : string.Empty);
            Refind();
        }
    }
}
