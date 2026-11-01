using System;

namespace DocuHelperEditor.State.Papers
{
    public enum SheetLinkCheck
    {
        Open,
        Unsafe,
        NotOpenable,
        TooLong,
    }

    public static class SheetLinkPolicy
    {
        public const int MaxLength = 2048;

        private static readonly string[] s_schemes = { "http", "https", "mailto" };

        public static SheetLinkCheck Check(string url, out string target)
        {
            target = null;
            var text = url?.Trim() ?? string.Empty;
            if (text.Length == 0)
                return SheetLinkCheck.NotOpenable;

            if (text.Length > MaxLength)
                return SheetLinkCheck.TooLong;

            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
                return SheetLinkCheck.NotOpenable;

            if (!Array.Exists(s_schemes, scheme => string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase)))
                return SheetLinkCheck.Unsafe;

            target = uri.AbsoluteUri;
            return SheetLinkCheck.Open;
        }
    }
}
