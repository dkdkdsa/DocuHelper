using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    internal sealed record HwpCommandSpec
    {
        public string Id { get; init; }

        public string Label { get; init; }

        public RibbonCommandClass Class { get; init; }

        public string Action { get; init; }

        public string Tool { get; init; }

        public string Args { get; init; }

        public string Prompt { get; init; }

        public string SelectionPrompt { get; init; }

        public string Reason { get; init; }

        public IReadOnlyList<string> Gates { get; init; } = Array.Empty<string>();

        public IReadOnlyList<HwpMenuSpec> Menu { get; init; } = Array.Empty<HwpMenuSpec>();

        public DialogSpec Dialog { get; init; }
    }
}
