using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed record CommandMenuSpec
    {
        public RibbonMenuKind Kind { get; init; }

        public string Label { get; init; }

        public string Detail { get; init; }

        public string Args { get; init; }

        public string Shortcut { get; init; }

        public string Font { get; init; }

        public string Palette { get; init; }

        public string Swatch { get; init; }

        public RibbonCommandClass Class { get; init; }

        public string Tool { get; init; }

        public string Prompt { get; init; }

        public string Reason { get; init; }

        public string Need { get; init; }

        public string Picker { get; init; }

        public IReadOnlyList<string> Gates { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Effects { get; init; }

        public DialogSpec Dialog { get; init; }
    }
}
