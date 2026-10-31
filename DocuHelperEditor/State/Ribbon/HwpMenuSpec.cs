using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    internal sealed record HwpMenuSpec
    {
        public HwpMenuKind Kind { get; init; }

        public string Label { get; init; }

        public string Args { get; init; }

        public string Detail { get; init; }

        public string Shortcut { get; init; }

        public string Font { get; init; }

        public string Palette { get; init; }

        public IReadOnlyList<string> Gates { get; init; } = Array.Empty<string>();

        public string Reason { get; init; }
    }
}
