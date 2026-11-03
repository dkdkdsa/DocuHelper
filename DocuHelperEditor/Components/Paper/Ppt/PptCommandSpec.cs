using System.Collections.Generic;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    internal sealed record PptCommandSpec
    {
        public string Id { get; init; }

        public PptCommandKind Kind { get; init; }

        public string Tool { get; init; }

        public string Args { get; init; }

        public string ArgsOff { get; init; }

        public PptNeed Need { get; init; }

        public string Prompt { get; init; }

        public string Reason { get; init; }

        public DialogSpec Dialog { get; init; }

        public string Pressed { get; init; }

        public IReadOnlyList<PptMenuSpec> Menu { get; init; }
    }
}
