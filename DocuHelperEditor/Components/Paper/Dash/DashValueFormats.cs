using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashValueFormats
    {
        public const string Default = "num";

        public static IReadOnlyList<(string Value, string Label)> All { get; } = new[]
        {
            ("man", "만 원"),
            ("won", "원"),
            ("gun", "건"),
            ("pct", "%"),
            ("day", "날짜"),
            ("num", "숫자"),
        };

        public static IReadOnlyList<(string Value, string Label)> With(string current)
        {
            if (string.IsNullOrWhiteSpace(current) || All.Any(format => string.Equals(format.Value, current, StringComparison.Ordinal)))
                return All;

            return All.Prepend((current, "지금 모양 · " + current)).ToList();
        }

        public static string Selected(string current) => string.IsNullOrWhiteSpace(current) ? Default : current;
    }
}
