using System.Text;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocText
    {
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var builder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    builder.Append(c).Append(text[i + 1]);
                    i++;
                }
                else if (c >= ' ' && !char.IsSurrogate(c) && c != '￾' && c != '￿')
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }
}
