using System.Collections.Generic;

namespace DocuHelper.Tools.Excel
{
    internal static class ShapeCatalog
    {
        private static readonly string[] s_presets =
        {
            "rect", "roundRect", "ellipse", "triangle", "rtTriangle", "diamond", "pentagon", "hexagon",
            "rightArrow", "leftArrow", "upArrow", "downArrow", "leftRightArrow", "star5", "heart", "cloud",
            "wedgeRectCallout", "lightningBolt", "line",
        };

        public static IReadOnlyList<string> Presets => s_presets;

        public static bool Contains(string geometry) => System.Array.IndexOf(s_presets, geometry) >= 0;
    }
}
