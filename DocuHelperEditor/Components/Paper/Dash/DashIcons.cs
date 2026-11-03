using System;
using System.Collections.Generic;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashIcons
    {
        public const string Page = "M4 4h16v16H4Z M4 9h16";

        public static readonly Dictionary<string, string> Elements = new(StringComparer.Ordinal)
        {
            ["text"] = "M4 7V4h16v3 M9 20h6 M12 4v16",
            ["shape"] = "M5 4h14a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1Z",
            ["line"] = "M4 12h16",
            ["chart"] = "M3 3v18h18 M7 16v-5 M12 16V8 M17 16v-9",
            ["table"] = "M3 5h18v14H3Z M3 10h18 M9 5v14",
            ["repeat"] = "M8 6h13 M8 12h13 M8 18h13 M3 6h.01 M3 12h.01 M3 18h.01",
            ["button"] = "M4 7h16v10H4Z M9 12h6",
            ["slider"] = "M3 12h18 M9 8v8",
            ["rangeSlider"] = "M3 12h18 M7 8v8 M17 8v8",
            ["toggle"] = "M7 7h10a5 5 0 0 1 0 10H7A5 5 0 0 1 7 7Z M16 12h.01",
            ["segmented"] = "M3 7h18v10H3Z M9 7v10 M15 7v10",
            ["select"] = "M4 7h16v10H4Z M13 11l2 2 2-2",
            ["meter"] = "M3 10h18v4H3Z M3 12h11",
        };

        public static string Of(string kind) => kind != null && Elements.TryGetValue(kind, out var path) ? path : Page;
    }
}
