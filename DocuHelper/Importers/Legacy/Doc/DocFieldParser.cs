using System;
using System.Collections.Generic;
using System.Text;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocFieldParser
    {
        public static string HyperlinkTarget(string instruction)
        {
            var tokens = Tokens(instruction);
            if (tokens.Count == 0 || !string.Equals(tokens[0], "HYPERLINK", StringComparison.OrdinalIgnoreCase))
                return null;

            string url = null;
            string anchor = null;
            for (var i = 1; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token.Length >= 2 && token[0] == '\\')
                {
                    var option = char.ToLowerInvariant(token[1]);
                    if (option == 'l' && i + 1 < tokens.Count)
                        anchor = tokens[++i];
                    else if ((option == 'o' || option == 't') && i + 1 < tokens.Count)
                        i++;
                }
                else if (url == null)
                {
                    url = token;
                }
            }

            if (url == null)
                return null;

            return anchor == null ? url : url + "#" + anchor;
        }

        private static List<string> Tokens(string text)
        {
            var tokens = new List<string>();
            var builder = new StringBuilder();
            var quoted = false;
            var started = false;
            foreach (var c in text ?? string.Empty)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                    started = true;
                }
                else if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (started)
                        tokens.Add(builder.ToString());

                    builder.Clear();
                    started = false;
                }
                else
                {
                    builder.Append(c);
                    started = true;
                }
            }

            if (started)
                tokens.Add(builder.ToString());

            return tokens;
        }
    }
}
