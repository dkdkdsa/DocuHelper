using System;
using System.Globalization;
using System.Linq;
using System.Text;
using DocuHelper.Tools.Excel;

namespace DocuHelper.Dash.Runtime
{
    public static class DashFormulaText
    {
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
