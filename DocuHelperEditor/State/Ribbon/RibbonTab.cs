using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed record RibbonTab
    {
        public string Key { get; init; }

        public string Title { get; init; }

        public IReadOnlyList<RibbonGroup> Groups { get; init; } = Array.Empty<RibbonGroup>();
    }
}
