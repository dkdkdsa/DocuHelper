using System.Text;
using System.Text.RegularExpressions;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal static class XlsXmlText
    {
        private static readonly Regex s_escapeLike = new("_x([0-9A-Fa-f]{4})_", RegexOptions.CultureInvariant);

        public static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? string.Empty;

            var value = s_escapeLike.Replace(text, "_x005F_x$1_");
            StringBuilder builder = null;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var bad = false;
                if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    builder?.Append(c).Append(value[i + 1]);
                    i++;
                    continue;
                }

                if (c < ' ' && c != '\t' && c != '\n' && c != '\r')
                    bad = true;
                else if (char.IsSurrogate(c) || c == '￾' || c == '￿')
                    bad = true;

                if (bad)
                {
                    builder ??= new StringBuilder(value, 0, i, value.Length + 16);
                    builder.Append("_x").Append(((int)c).ToString("X4")).Append('_');
                }
                else
                {
                    builder?.Append(c);
                }
            }

            return builder?.ToString() ?? value;
        }

        public static bool IsClean(string text)
        {
            foreach (var c in text)
            {
                if ((c < ' ' && c != '\t' && c != '\n' && c != '\r') || char.IsSurrogate(c) || c == '￾' || c == '￿')
                    return false;
            }

            return true;
        }
    }
}
