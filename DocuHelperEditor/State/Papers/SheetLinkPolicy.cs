using System;

namespace DocuHelperEditor.State.Papers
{
    public static class SheetLinkPolicy
    {
        private static readonly string[] s_schemes = { "http", "https", "mailto" };

        public static bool TryOpenable(string url, out string target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
                return false;

            if (Array.Exists(s_schemes, scheme => string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase)))
                target = uri.AbsoluteUri;

            return target != null;
        }
    }
}
