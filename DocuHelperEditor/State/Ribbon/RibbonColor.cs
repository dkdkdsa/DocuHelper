using System;

namespace DocuHelperEditor.State.Ribbon
{
    public static class RibbonColor
    {
        public static string ToCss(string color)
        {
            if (string.IsNullOrWhiteSpace(color))
                return null;

            var text = color.Trim();
            if (IsHex(text))
                return "#" + text;

            if (text.IndexOfAny(new[] { ';', '{', '}', '"', '\'', '\\', '<', '>' }) >= 0)
                return null;

            return text;
        }

        public static bool SameColor(string left, string right)
        {
            var a = Strip(left);
            var b = Strip(right);
            return a.Length > 0 && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static string Strip(string color) => (color ?? string.Empty).Trim().TrimStart('#');

        private static bool IsHex(string text)
        {
            if (text.Length != 3 && text.Length != 4 && text.Length != 6 && text.Length != 8)
                return false;

            foreach (var c in text)
            {
                var hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex)
                    return false;
            }

            return true;
        }
    }
}
