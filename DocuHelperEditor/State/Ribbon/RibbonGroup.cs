using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed record RibbonGroup
    {
        public string Title { get; init; }

        public RibbonGroupLayout Layout { get; init; }

        public IReadOnlyList<RibbonItem> Items { get; init; } = Array.Empty<RibbonItem>();

        public IReadOnlyList<RibbonItem> Row1 { get; init; } = Array.Empty<RibbonItem>();

        public IReadOnlyList<RibbonItem> Row2 { get; init; } = Array.Empty<RibbonItem>();
    }
}
