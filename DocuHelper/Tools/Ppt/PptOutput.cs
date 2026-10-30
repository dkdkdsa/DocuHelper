using System;
using System.Globalization;

namespace DocuHelper.Tools.Ppt
{
    public static class PptOutput
    {
        public static string Points(double size) => size.ToString("0.#", CultureInfo.InvariantCulture) + "pt";

        public static string Number(double value) => value.ToString("#,0.##", CultureInfo.InvariantCulture);

        public static string Inline(string text) => (text ?? string.Empty).Replace('\n', ' ').Trim();

        public static string Shorten(string text, int max)
        {
            var value = Inline(text);
            return value.Length <= max ? value : value.Substring(0, Math.Max(1, max - 1)).TrimEnd() + "…";
        }

        public static string ShapeLabel(PptShapeInfo shape)
        {
            var role = PptPlaceholder.RoleName(shape.Role);
            if (shape.Role == PptShapeRole.Body && shape.PlaceholderType != null && shape.PlaceholderIndex > 1)
                role += " " + shape.PlaceholderIndex.ToString(CultureInfo.InvariantCulture);

            return $"[{shape.ShapeId.ToString(CultureInfo.InvariantCulture)}] {role}";
        }

        public static string Fit(PptFitResult fit)
        {
            if (fit == null || !fit.IsKnown)
                return "size unknown, not checked";

            if (fit.TooWide)
                return "a line is wider than the box (text does not wrap in this box)";

            var room = $"about {fit.LinesNeeded} line{Plural(fit.LinesNeeded)} at {Points(fit.FontSize)}, room for about {fit.LinesAvailable}";
            if (fit.Fits)
                return $"fits ({room})";

            var text = $"TOO MUCH TEXT ({room}, {Math.Round(fit.Ratio * 100).ToString(CultureInfo.InvariantCulture)}% of the box)";
            return fit.AutoShrink ? text + "; PowerPoint will shrink the text to squeeze it in" : text;
        }

        public static string Plural(int count) => count == 1 ? string.Empty : "s";
    }
}
