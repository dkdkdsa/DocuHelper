using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed record RibbonMenuItem
    {
        public RibbonMenuKind Kind { get; init; }

        public string Value { get; init; }

        public string Label { get; init; }

        public string Detail { get; init; }

        public string Shortcut { get; init; }

        public string Icon { get; init; }

        public string Swatch { get; init; }

        public string FontFamily { get; init; }

        public bool? Checked { get; init; }

        public bool Disabled { get; init; }

        public string Reason { get; init; }

        public IReadOnlyList<RibbonSwatch> Swatches { get; init; } = Array.Empty<RibbonSwatch>();

        public string Current { get; init; }

        public int Columns { get; init; } = 5;

        public string Hint { get; init; }
    }
}
