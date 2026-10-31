using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    public static class ConditionalIconSets
    {
        public static readonly IReadOnlyList<string> Names = new[]
        {
            "3Arrows", "3ArrowsGray", "3Flags", "3TrafficLights1", "3TrafficLights2", "3Signs", "3Symbols", "3Symbols2",
            "4Arrows", "4ArrowsGray", "4RedToBlack", "4Rating", "4TrafficLights",
            "5Arrows", "5ArrowsGray", "5Rating", "5Quarters",
        };

        public static string Find(string name)
        {
            var key = (name ?? string.Empty).Trim();
            return Names.FirstOrDefault(n => string.Equals(n, key, StringComparison.OrdinalIgnoreCase));
        }

        public static int CountOf(string name) =>
            !string.IsNullOrEmpty(name) && name[0] >= '3' && name[0] <= '5' ? name[0] - '0' : 3;

        public static int[] DefaultPercents(int count) => count switch
        {
            4 => new[] { 0, 25, 50, 75 },
            5 => new[] { 0, 20, 40, 60, 80 },
            _ => new[] { 0, 33, 67 },
        };
    }
}
