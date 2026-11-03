using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash;
using DocuHelper.Dash.Elements;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed partial class DashRibbonCommands
    {
        private const string NoSelectionReason = "먼저 요소를 골라 주세요.";
        private const string OneOnlyReason = "요소를 하나만 골라 주세요.";

        private IReadOnlyList<string> _select;

        private Dictionary<string, Func<CommandSpec, string>> NeedHandlers() => new(StringComparer.Ordinal)
        {
            [DashCommandData.ModelNeed] = _ => Model == null ? NoModelReason : null,
            [DashCommandData.SelectionNeed] = _ => SelectedElements().Count == 0 ? NoSelectionReason : null,
            [DashCommandData.OneNeed] = _ => SelectedElements().Count switch
            {
                0 => NoSelectionReason,
                1 => _state.Dialogs == null ? PendingReason : null,
                _ => OneOnlyReason,
            },
        };

        protected override void RefreshSelection()
        {
            var model = Model;
            var ids = _select;
            _select = null;
            if (model == null)
            {
                _state.Selected.Clear();
                return;
            }

            if (ids != null)
            {
                _state.Selected.Clear();
                _state.Selected.AddRange(ids.Where(id => model.Document.Find(id) != null));
                _state.Mode = DashViewState.EditMode;
                _state.EditingId = null;
                return;
            }

            _state.Selected.RemoveAll(id => model.Document.Find(id) == null);
        }

        private IReadOnlyList<Element> SelectedElements()
        {
            var document = Model?.Document;
            if (document == null)
                return Array.Empty<Element>();

            return _state.Selected
                .Select(document.Find)
                .Where(element => element != null && element != document.Root)
                .Distinct()
                .ToList();
        }

        private static IReadOnlyList<Element> Outermost(IReadOnlyList<Element> elements)
        {
            var inner = new HashSet<Element>();
            foreach (var element in elements)
            {
                foreach (var child in Descendants(element))
                    inner.Add(child);
            }

            return elements.Where(element => !inner.Contains(element)).ToList();
        }

        private static IEnumerable<Element> Descendants(Element element)
        {
            foreach (var child in element.Nested().Where(child => child != null))
            {
                yield return child;
                foreach (var deeper in Descendants(child))
                    yield return deeper;
            }
        }

        private static Element ParentOf(DashDocument document, Element element) =>
            document.Walk().FirstOrDefault(candidate => candidate.Nested().Contains(element));
    }
}
