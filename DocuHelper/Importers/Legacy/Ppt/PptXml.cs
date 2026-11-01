using System.Text;
using System.Xml;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal static class PptXml
    {
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    builder.Append(c).Append(text[i + 1]);
                    i++;
                }
                else if (XmlConvert.IsXmlChar(c))
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        public static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length + 8);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    builder.Append(c).Append(text[i + 1]);
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '&':
                        builder.Append("&amp;");
                        break;
                    case '<':
                        builder.Append("&lt;");
                        break;
                    case '>':
                        builder.Append("&gt;");
                        break;
                    case '"':
                        builder.Append("&quot;");
                        break;
                    default:
                        if (XmlConvert.IsXmlChar(c))
                            builder.Append(c);

                        break;
                }
            }

            return builder.ToString();
        }
    }
}
