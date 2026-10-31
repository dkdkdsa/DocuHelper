using System;
using System.Collections.Generic;
using System.Text;

namespace DocuHelper.Tools.Excel
{
    internal static class HeaderFooterCodes
    {
        public const int MaxLength = 255;

        private static readonly (string Placeholder, char Code)[] s_fields =
        {
            ("{page}", 'P'),
            ("{pages}", 'N'),
            ("{date}", 'D'),
            ("{time}", 'T'),
            ("{file}", 'F'),
            ("{sheet}", 'A'),
        };

        public static string[] Split(string raw)
        {
            var parts = new[] { string.Empty, string.Empty, string.Empty };
            if (string.IsNullOrEmpty(raw))
                return parts;

            var builders = new[] { new StringBuilder(), new StringBuilder(), new StringBuilder() };
            var current = 1;
            for (var i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                if (c != '&' || i + 1 >= raw.Length)
                {
                    builders[current].Append(c);
                    continue;
                }

                var next = raw[i + 1];
                if (next == 'L' || next == 'C' || next == 'R')
                {
                    current = next == 'L' ? 0 : next == 'C' ? 1 : 2;
                    i++;
                    continue;
                }

                builders[current].Append(c).Append(next);
                i++;
                if (next == '"')
                {
                    var close = raw.IndexOf('"', i + 1);
                    var end = close < 0 ? raw.Length - 1 : close;
                    builders[current].Append(raw, i + 1, end - i);
                    i = end;
                }
            }

            for (var i = 0; i < 3; i++)
                parts[i] = builders[i].ToString();

            return parts;
        }

        public static string Join(string left, string center, string right)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(left))
                builder.Append("&L").Append(left);

            if (!string.IsNullOrEmpty(center))
                builder.Append("&C").Append(center);

            if (!string.IsNullOrEmpty(right))
                builder.Append("&R").Append(right);

            return builder.ToString();
        }

        public static string Decode(string section)
        {
            if (string.IsNullOrEmpty(section))
                return string.Empty;

            var builder = new StringBuilder();
            for (var i = 0; i < section.Length; i++)
            {
                var c = section[i];
                if (c != '&' || i + 1 >= section.Length)
                {
                    builder.Append(c);
                    continue;
                }

                var next = section[++i];
                if (next == '&')
                {
                    builder.Append('&');
                    continue;
                }

                var field = Array.Find(s_fields, f => f.Code == next);
                if (field.Placeholder != null)
                {
                    builder.Append(field.Placeholder);
                    continue;
                }

                builder.Append('&').Append(next);
            }

            return builder.ToString();
        }

        public static string Encode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    var matched = false;
                    foreach (var field in s_fields)
                    {
                        if (string.Compare(text, i, field.Placeholder, 0, field.Placeholder.Length, StringComparison.OrdinalIgnoreCase) != 0)
                            continue;

                        builder.Append('&').Append(field.Code);
                        i += field.Placeholder.Length - 1;
                        matched = true;
                        break;
                    }

                    if (matched)
                        continue;
                }

                builder.Append(text[i]);
                if (text[i] == '&')
                    builder.Append('&');
            }

            return builder.ToString();
        }

        public static IReadOnlyList<string> Placeholders() => Array.ConvertAll(s_fields, f => f.Placeholder);
    }
}
