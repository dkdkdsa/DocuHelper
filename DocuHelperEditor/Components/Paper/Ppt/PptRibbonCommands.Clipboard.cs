using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public sealed partial class PptRibbonCommands
    {
        private const string PasteEmptyText = "붙여넣을 글자가 없어요.";
        private const string PasteLinesText = "글자가 길어서 앞부분 {0}줄만 붙였어요.";
        private const string PasteCharsText = "글자가 4000자를 넘어서 앞부분만 붙였어요.";
        private const string PainterOnText = "서식을 복사했어요. 이어서 고른 글상자에 붙여요.";
        private const string PainterOffText = "서식 복사를 껐어요.";
        private const int MaxPasteLines = 60;
        private const int MaxPasteCharacters = 4000;

        private FormatCopy _painter;

        private sealed record FormatCopy(int SlideNumber, string ShapeKey, double PointSize, bool Bold, bool Italic, bool Underline, string Align);

        public void SelectionChanged()
        {
            if (_painter == null)
                return;

            var model = _state.ModelSource?.Invoke();
            var box = model?.SelectedBox;
            if (box == null || string.IsNullOrEmpty(box.ShapeKey))
                return;

            var slide = _state.Selection.SlideNumber;
            if (slide == _painter.SlideNumber && box.ShapeKey == _painter.ShapeKey)
                return;

            var copy = _painter;
            _painter = null;
            var args = new Dictionary<string, object>
            {
                ["slide"] = slide,
                ["shape"] = box.ShapeKey,
                ["bold"] = copy.Bold,
                ["italic"] = copy.Italic,
                ["underline"] = copy.Underline,
                ["align"] = copy.Align,
            };
            if (copy.PointSize > 0)
                args["size"] = copy.PointSize;

            Apply(EditCommand.Create("set_font", args));
        }

        private Task CopyAsync(bool cut)
        {
            _state.CommitDraft?.Invoke();
            var box = _state.ModelSource?.Invoke()?.SelectedBox;
            if (box == null || string.IsNullOrEmpty(box.ShapeKey))
                return Task.CompletedTask;

            var text = string.Join("\n", box.Lines ?? new List<string>());
            return CopyTextAsync(text, cut, box.ShapeKey);
        }

        private async Task CopyTextAsync(string text, bool cut, string shape)
        {
            await _clipboard.SetTextAsync(text);
            if (cut)
                Apply(EditCommand.Create("set_slide_text", new { slide = _state.Selection.SlideNumber, shape, lines = new[] { string.Empty } }));
        }

        private async Task PasteAsync()
        {
            _state.CommitDraft?.Invoke();
            var box = _state.ModelSource?.Invoke()?.SelectedBox;
            if (box == null || string.IsNullOrEmpty(box.ShapeKey))
                return;

            var lines = PastedLines(await _clipboard.GetTextAsync(), out var notice);
            if (lines.Count == 0)
            {
                Notify(PasteEmptyText);
                return;
            }

            if (notice != null)
                Notify(notice);

            Apply(EditCommand.Create("set_slide_text", new { slide = _state.Selection.SlideNumber, shape = box.ShapeKey, lines = lines.ToArray() }));
        }

        private Task FormatPainterAsync()
        {
            if (_painter != null)
            {
                _painter = null;
                Notify(PainterOffText);
                return Task.CompletedTask;
            }

            _state.CommitDraft?.Invoke();
            var box = _state.ModelSource?.Invoke()?.SelectedBox;
            if (box == null || string.IsNullOrEmpty(box.ShapeKey))
                return Task.CompletedTask;

            _painter = new FormatCopy(
                _state.Selection.SlideNumber,
                box.ShapeKey,
                box.PointSize,
                box.Bold,
                box.Italic,
                box.Underline,
                box.Align switch { SlideTextAlign.Center => "center", SlideTextAlign.Right => "right", SlideTextAlign.Justify => "justify", _ => "left" });
            Notify(PainterOnText);
            return Task.CompletedTask;
        }

        private static List<string> PastedLines(string text, out string notice)
        {
            notice = null;
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return lines;

            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            if (normalized.Length > MaxPasteCharacters)
            {
                normalized = normalized.Substring(0, MaxPasteCharacters);
                notice = PasteCharsText;
            }

            var clean = new string(normalized.Where(c => c == '\n' || !char.IsControl(c)).ToArray());
            lines.AddRange(clean.Split('\n'));
            if (lines.Count > MaxPasteLines)
            {
                lines.RemoveRange(MaxPasteLines, lines.Count - MaxPasteLines);
                notice = string.Format(CultureInfo.InvariantCulture, PasteLinesText, MaxPasteLines);
            }

            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0)
                lines.RemoveAt(lines.Count - 1);

            return lines;
        }
    }
}
