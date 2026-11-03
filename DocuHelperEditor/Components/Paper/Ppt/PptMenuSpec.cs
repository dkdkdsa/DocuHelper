using System.Collections.Generic;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    internal sealed record PptMenuSpec
    {
        public string Label { get; init; }

        public string Detail { get; init; }

        public string Args { get; init; }

        public string Tool { get; init; }

        public PptCommandKind Kind { get; init; }

        public string Prompt { get; init; }

        public string Reason { get; init; }

        public DialogSpec Dialog { get; init; }

        public string Picker { get; init; }

        public string Swatch { get; init; }

        public IReadOnlyList<string> Effects { get; init; }

        public PptNeed Need { get; init; }
    }
}
