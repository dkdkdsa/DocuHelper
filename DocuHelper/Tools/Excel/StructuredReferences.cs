using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocuHelper.Tools.Excel
{
    internal static class StructuredReferences
    {
        public static string Column(string name)
        {
            var builder = new StringBuilder("[");
            foreach (var c in name)
            {
                if (c is '[' or ']' or '#' or '\'')
                    builder.Append('\'');

                builder.Append(c);
            }

            return builder.Append(']').ToString();
        }

        public static bool Mentions(string formula, ExcelTable table, bool inside) => Spans(formula, table, inside).Count > 0;

        public static bool MentionsAnywhere(string text, ExcelTable table)
        {
            foreach (var name in Names(table))
            {
                if (text.Contains(name + "[", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static string ToCells(string formula, ExcelTable table, ExcelSheet host, int row, int column, out string failure)
        {
            failure = null;
            var inside = host != null && host == table.Sheet && table.Contains(column, row);
            var spans = Spans(formula, table, inside);
            if (spans.Count == 0)
                return formula;

            var builder = new StringBuilder();
            var cursor = 0;
            foreach (var span in spans)
            {
                builder.Append(formula, cursor, span.Start - cursor);
                if (!Resolve(span.Specifier, table, host, row, column, out var cells))
                {
                    failure = formula.Substring(span.Start, span.End - span.Start);
                    return null;
                }

                builder.Append(cells);
                cursor = span.End;
            }

            builder.Append(formula, cursor, formula.Length - cursor);
            return builder.ToString();
        }

        private static IEnumerable<string> Names(ExcelTable table)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in new[] { table.Definition.DisplayName?.Value, table.Definition.Name?.Value })
            {
                if (!string.IsNullOrEmpty(name) && seen.Add(name))
                    yield return name;
            }
        }

        private static List<Span> Spans(string text, ExcelTable table, bool inside)
        {
            var spans = new List<Span>();
            if (string.IsNullOrEmpty(text))
                return spans;

            var names = Names(table).ToList();
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (c == '"' || c == '\'')
                {
                    i = SkipQuoted(text, i, c);
                    continue;
                }

                if (c == '[')
                {
                    var end = GroupEnd(text, i);
                    if (end < 0)
                        break;

                    if (inside)
                        spans.Add(new Span(i, end, text.Substring(i + 1, end - i - 2)));

                    i = end;
                    continue;
                }

                if (IsWordChar(c))
                {
                    var start = i;
                    while (i < text.Length && IsWordChar(text[i]))
                        i++;

                    if (i < text.Length && text[i] == '[' && names.Contains(text.Substring(start, i - start), StringComparer.OrdinalIgnoreCase))
                    {
                        var end = GroupEnd(text, i);
                        if (end < 0)
                            break;

                        spans.Add(new Span(start, end, text.Substring(i + 1, end - i - 2)));
                        i = end;
                    }

                    continue;
                }

                i++;
            }

            return spans;
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '\\';

        private static int SkipQuoted(string text, int start, char quote)
        {
            var i = start + 1;
            while (i < text.Length)
            {
                if (text[i] == quote)
                {
                    if (i + 1 < text.Length && text[i + 1] == quote)
                    {
                        i += 2;
                        continue;
                    }

                    return i + 1;
                }

                i++;
            }

            return text.Length;
        }

        private static int GroupEnd(string text, int start)
        {
            var depth = 0;
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\'')
                {
                    i++;
                    continue;
                }

                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i + 1;
                }
            }

            return -1;
        }

        private static List<string> SplitTop(string text)
        {
            var items = new List<string>();
            var depth = 0;
            var from = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\'')
                    i++;
                else if (c == '[')
                    depth++;
                else if (c == ']')
                    depth--;
                else if (c == ',' && depth == 0)
                {
                    items.Add(text.Substring(from, i - from));
                    from = i + 1;
                }
            }

            items.Add(text.Substring(from));
            return items;
        }

        private static string Unescape(string text)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\'' && i + 1 < text.Length)
                    i++;

                builder.Append(text[i]);
            }

            return builder.ToString();
        }

        private static bool Resolve(string specifier, ExcelTable table, ExcelSheet host, int hostRow, int hostColumn, out string cells)
        {
            cells = null;
            var columns = table.Columns;
            var first = -1;
            var last = -1;
            var thisRow = false;
            var all = false;
            var data = false;
            var headers = false;
            var totals = false;

            bool SetColumns(string from, string to)
            {
                var a = IndexOf(columns, Unescape(from));
                var b = IndexOf(columns, Unescape(to));
                if (a < 0 || b < 0)
                    return false;

                first = Math.Min(a, b);
                last = Math.Max(a, b);
                return true;
            }

            foreach (var raw in SplitTop(specifier))
            {
                var item = raw.Trim();
                if (item.StartsWith('@'))
                {
                    thisRow = true;
                    item = item.Substring(1).Trim();
                    if (item.Length == 0)
                        continue;
                }

                if (item.Length == 0)
                    continue;

                if (item[0] == '[')
                {
                    var end = GroupEnd(item, 0);
                    if (end < 0)
                        return false;

                    var content = item.Substring(1, end - 2);
                    var rest = item.Substring(end).Trim();
                    if (rest.Length == 0)
                    {
                        if (content.StartsWith('#'))
                        {
                            if (!Special(content, ref all, ref data, ref headers, ref totals, ref thisRow))
                                return false;
                        }
                        else if (!SetColumns(content, content))
                            return false;

                        continue;
                    }

                    if (rest[0] != ':')
                        return false;

                    rest = rest.Substring(1).Trim();
                    if (rest.Length == 0 || rest[0] != '[')
                        return false;

                    var secondEnd = GroupEnd(rest, 0);
                    if (secondEnd != rest.Length || !SetColumns(content, rest.Substring(1, secondEnd - 2)))
                        return false;

                    continue;
                }

                if (item[0] == '#')
                {
                    if (!Special(item, ref all, ref data, ref headers, ref totals, ref thisRow))
                        return false;

                    continue;
                }

                if (!SetColumns(item, item))
                    return false;
            }

            if (first < 0)
            {
                first = 0;
                last = columns.Count - 1;
            }

            int top;
            int bottom;
            var range = table.Range;
            if (thisRow)
            {
                if (all || data || headers || totals || host != table.Sheet || hostRow < range.Top || hostRow > range.Bottom || (table.HasHeader && hostRow == range.Top) || (table.HasTotals && hostRow == range.Bottom))
                    return false;

                top = hostRow;
                bottom = hostRow;
            }
            else if (all)
            {
                top = range.Top;
                bottom = range.Bottom;
            }
            else
            {
                if (!data && !headers && !totals)
                    data = true;

                var parts = new List<(int Top, int Bottom)>();
                if (headers)
                {
                    if (!table.HasHeader)
                        return false;

                    parts.Add((range.Top, range.Top));
                }

                if (data)
                {
                    if (table.DataRange == null)
                        return false;

                    parts.Add((table.DataRange.Top, table.DataRange.Bottom));
                }

                if (totals)
                {
                    if (!table.HasTotals)
                        return false;

                    parts.Add((range.Bottom, range.Bottom));
                }

                parts.Sort();
                for (var k = 1; k < parts.Count; k++)
                {
                    if (parts[k].Top != parts[k - 1].Bottom + 1)
                        return false;
                }

                top = parts[0].Top;
                bottom = parts[^1].Bottom;
            }

            var area = new CellRange(range.Left + first, top, range.Left + last, bottom);
            cells = (host == table.Sheet ? string.Empty : FormulaReference.QuoteSheet(table.Sheet.Name) + "!") + area;
            return true;
        }

        private static bool Special(string text, ref bool all, ref bool data, ref bool headers, ref bool totals, ref bool thisRow)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "#all":
                    all = true;
                    return true;
                case "#data":
                    data = true;
                    return true;
                case "#headers":
                    headers = true;
                    return true;
                case "#totals":
                    totals = true;
                    return true;
                case "#this row":
                    thisRow = true;
                    return true;
                default:
                    return false;
            }
        }

        private static int IndexOf(IReadOnlyList<string> columns, string name)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (string.Equals(columns[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private readonly struct Span
        {
            public Span(int start, int end, string specifier)
            {
                Start = start;
                End = end;
                Specifier = specifier;
            }

            public int Start { get; }

            public int End { get; }

            public string Specifier { get; }
        }
    }
}
