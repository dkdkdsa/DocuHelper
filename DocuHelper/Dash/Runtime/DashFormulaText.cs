using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using DocuHelper.Tools.Excel;

namespace DocuHelper.Dash.Runtime
{
    public static class DashFormulaText
    {
        private const string ColumnAlias = "QuotedColumn";

        private static readonly Regex s_bracket = new(@"""(?:[^""]|"""")*""|\[([^\[\]]+)\]", RegexOptions.CultureInvariant);

        public static string Strip(string expression)
        {
            var text = (expression ?? string.Empty).Trim();
            return text.StartsWith('=') ? text.Substring(1).TrimStart() : text;
        }

        public static string ReplaceNames(string expression, Func<string, string> replace)
        {
            var text = Strip(expression);
            var tokens = FormulaLexer.Tokenize(text);
            var builder = new StringBuilder(text);
            foreach (var token in tokens.Where(token => token.Kind == FormulaTokenKind.Name).OrderByDescending(token => token.Start))
            {
                var replacement = replace(token.Text);
                if (replacement == null)
                    continue;

                builder.Remove(token.Start, token.End - token.Start);
                builder.Insert(token.Start, replacement);
            }

            return builder.ToString();
        }

        public static QuotedColumns QuoteColumns(string expression, IReadOnlyList<string> columns)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            var text = s_bracket.Replace(expression ?? string.Empty, match =>
            {
                if (!match.Groups[1].Success)
                    return match.Value;

                var name = match.Groups[1].Value.Trim();
                var at = IndexOf(columns, name);
                if (at < 0)
                    throw new DashException($"[{name}] 칸을 찾을 수 없어요. 있는 칸: {string.Join(", ", columns)}");

                var alias = ColumnAlias + at.ToString(CultureInfo.InvariantCulture);
                map[alias] = at;
                return alias;
            });
            return new QuotedColumns(text, map);
        }

        private static int IndexOf(IReadOnlyList<string> columns, string name)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (string.Equals(columns[i]?.Trim(), name, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public static string Literal(object value) => value switch
        {
            null => "\"\"",
            bool flag => flag ? "TRUE" : "FALSE",
            DateTime date => ExcelValue.Invariant(date.ToOADate()),
            double number => Number(number),
            int number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            DashError error => error.Code,
            _ => "\"" + DashCells.Text(value).Replace("\"", "\"\"") + "\"",
        };

        private static string Number(double number)
        {
            var text = ExcelValue.Invariant(number);
            return number < 0 ? "(" + text + ")" : text;
        }
    }
}
