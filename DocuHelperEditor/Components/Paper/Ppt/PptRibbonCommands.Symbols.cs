using System.Collections.Generic;
using System.Linq;
using DocuHelper.Editors;
using DocuHelperEditor.State.Ribbon;
using static DocuHelperEditor.State.Ribbon.RibbonMenus;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public sealed partial class PptRibbonCommands
    {
        private const string SymbolId = "ppt.insert.symbol";
        private const string SymbolHeading = "자주 쓰는 기호";

        private static readonly string[] s_symbols =
        {
            "★", "☆", "♥", "♪", "→", "←", "↑", "↓", "※", "©", "®", "™", "…", "·", "◎", "○", "●", "□", "■", "△", "▲", "✓", "±", "×", "÷", "≠", "≤", "≥", "∞", "°", "€", "₩", "¥", "§",
        };

        private void RegisterSymbol()
        {
            AddTool(SymbolId, symbol => SymbolEdit(symbol))
                .Unless(() => NeedBlocker(PptNeed.Text))
                .MenuFrom(SymbolMenu);
        }

        private IReadOnlyList<RibbonMenuItem> SymbolMenu()
        {
            var items = new List<RibbonMenuItem> { Heading(SymbolHeading) };
            items.AddRange(s_symbols.Select(symbol => Item(symbol, symbol)));
            return items;
        }

        private EditCommand SymbolEdit(string symbol)
        {
            if (string.IsNullOrEmpty(symbol) || !s_symbols.Contains(symbol))
                return null;

            _state.CommitDraft?.Invoke();
            var selection = _state.Selection;
            var box = _state.ModelSource?.Invoke()?.SelectedBox;
            if (box == null || string.IsNullOrEmpty(box.ShapeKey))
            {
                Notify(NoTextReason);
                return null;
            }

            var lines = (box.Lines ?? new List<string>()).ToList();
            if (lines.Count == 0)
                lines.Add(string.Empty);

            lines[lines.Count - 1] += symbol;
            return EditCommand.Create("set_slide_text", new { slide = selection.SlideNumber, shape = box.ShapeKey, lines = lines.ToArray() });
        }
    }
}
