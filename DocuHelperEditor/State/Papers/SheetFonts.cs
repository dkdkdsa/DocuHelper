using System;
using System.Collections.Generic;
using System.Globalization;

namespace DocuHelperEditor.State.Papers
{
    public static class SheetFonts
    {
        public const double PixelsPerPoint = 96.0 / 72.0;

        private const string Fallback = "'Malgun Gothic', var(--font-body)";

        private static readonly Dictionary<string, string> s_aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["맑은 고딕"] = "Malgun Gothic",
            ["굴림"] = "Gulim",
            ["굴림체"] = "GulimChe",
            ["돋움"] = "Dotum",
            ["돋움체"] = "DotumChe",
            ["바탕"] = "Batang",
            ["바탕체"] = "BatangChe",
            ["궁서"] = "Gungsuh",
            ["궁서체"] = "GungsuhChe",
            ["나눔고딕"] = "NanumGothic",
            ["나눔명조"] = "NanumMyeongjo",
        };

        public static string Family(string name, string inherited = null)
        {
            var fallback = string.IsNullOrWhiteSpace(inherited) ? Fallback : Family(inherited);
            if (string.IsNullOrWhiteSpace(name))
                return fallback;

            var clean = name.Replace("'", string.Empty).Replace("\"", string.Empty).Replace(";", string.Empty).Trim();
            var family = "'" + clean + "'";
            if (s_aliases.TryGetValue(clean, out var alias))
                family += ", '" + alias + "'";

            return family + ", " + fallback;
        }

        public static string Pixels(double points) => (points * PixelsPerPoint).ToString("0.##", CultureInfo.InvariantCulture) + "px";
    }
}
