using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocuHelperEditor.State.Papers
{
    public static class SheetTsv
    {
        private const string RowBreak = "\r\n";

        public static string Format(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            var builder = new StringBuilder();
            foreach (var row in rows)
            {
                builder.Append(string.Join("\t", row.Select(Quote)));
                builder.Append(RowBreak);
            }

            return builder.ToString();
        }

        public static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(text))
                return rows;

            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            var started = false;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        cell.Append(ch);
                    }

                    continue;
                }

                if (ch == '"' && !started)
                {
                    quoted = true;
                    started = true;
                    continue;
                }

                if (ch == '\t')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    started = false;
                    continue;
                }

                if (ch == '\r' || ch == '\n')
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    row.Add(cell.ToString());
                    rows.Add(row);
                    row = new List<string>();
                    cell.Clear();
                    started = false;
                    continue;
                }

                cell.Append(ch);
                started = true;
            }

            if (cell.Length > 0 || row.Count > 0 || started)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            var width = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
            foreach (var r in rows)
            {
                while (r.Count < width)
                    r.Add(string.Empty);
            }

            return rows;
        }

        public static string Normalize(string text) => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');

        private static string Quote(string value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny(new[] { '\t', '\n', '\r', '"' }) < 0)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
