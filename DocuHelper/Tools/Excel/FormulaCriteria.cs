using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DocuHelper.Tools.Excel
{
    internal sealed class FormulaCriteria
    {
        private const int BlankTarget = 0;
        private const int NumberTarget = 1;
        private const int TextTarget = 2;
        private const int BooleanTarget = 3;
        private const int ErrorTarget = 4;

        private static readonly string[] s_operators = { "<=", ">=", "<>", "<", ">", "=" };
        private static readonly char[] s_wildcards = { '*', '?', '~' };

        private readonly string _op;
        private readonly int _target;
        private readonly double _number;
        private readonly string _text;
        private readonly Regex _pattern;

        private FormulaCriteria(string op, int target, double number, string text)
        {
            _op = op;
            _target = target;
            _number = number;
            _text = text;
            if (target == TextTarget && (op == "=" || op == "<>") && text.IndexOfAny(s_wildcards) >= 0)
                _pattern = BuildPattern(text);
        }

        public static FormulaCriteria From(ExcelValue criteria)
        {
            switch (criteria.Kind)
            {
                case ExcelValueKind.Number:
                    return new FormulaCriteria("=", NumberTarget, criteria.Number, null);
                case ExcelValueKind.Boolean:
                    return new FormulaCriteria("=", BooleanTarget, criteria.Boolean ? 1 : 0, null);
                case ExcelValueKind.Error:
                    return new FormulaCriteria("=", ErrorTarget, 0, criteria.Text);
                case ExcelValueKind.Blank:
                    return new FormulaCriteria("=", BlankTarget, 0, null);
            }

            var text = criteria.Text ?? string.Empty;
            var op = "=";
            foreach (var candidate in s_operators)
            {
                if (text.StartsWith(candidate, StringComparison.Ordinal))
                {
                    op = candidate;
                    text = text.Substring(candidate.Length);
                    break;
                }
            }

            if (text.Length == 0)
                return new FormulaCriteria(op == "<>" ? "<>" : "=", BlankTarget, 0, null);

            if (TypedInput.TryParseNumber(text, out var number, out _))
                return new FormulaCriteria(op, NumberTarget, number, text);

            if (string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "FALSE", StringComparison.OrdinalIgnoreCase))
                return new FormulaCriteria(op, BooleanTarget, string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase) ? 1 : 0, null);

            if (text.StartsWith("#", StringComparison.Ordinal) && (text.EndsWith("!", StringComparison.Ordinal) || text.EndsWith("?", StringComparison.Ordinal) || text == ExcelValue.NotAvailable))
                return new FormulaCriteria(op, ErrorTarget, 0, text);

            return new FormulaCriteria(op, TextTarget, 0, text);
        }

        public bool Matches(ExcelValue value)
        {
            switch (_target)
            {
                case BlankTarget:
                    var empty = value.IsBlank || (value.IsText && value.Text.Length == 0);
                    return _op == "<>" ? !empty : empty;
                case NumberTarget:
                    if (!value.IsNumber)
                        return _op == "<>";

                    return Compare(ExcelValue.Round15(value.Number).CompareTo(ExcelValue.Round15(_number)));
                case BooleanTarget:
                    if (!value.IsBoolean)
                        return _op == "<>";

                    return Compare((value.Boolean ? 1 : 0).CompareTo((int)_number));
                case ErrorTarget:
                    var same = value.IsError && string.Equals(value.Text, _text, StringComparison.OrdinalIgnoreCase);
                    return _op == "<>" ? !same : same;
                default:
                    if (_op == "=" || _op == "<>")
                    {
                        var match = value.IsText && TextMatches(value.Text);
                        return _op == "=" ? match : !match;
                    }

                    if (!value.IsText)
                        return false;

                    return Compare(string.Compare(value.Text, _text, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase));
            }
        }

        private bool Compare(int comparison) => _op switch
        {
            "=" => comparison == 0,
            "<>" => comparison != 0,
            "<" => comparison < 0,
            ">" => comparison > 0,
            "<=" => comparison <= 0,
            _ => comparison >= 0,
        };

        private bool TextMatches(string text) =>
            _pattern != null ? _pattern.IsMatch(text) : string.Equals(text, _text, StringComparison.OrdinalIgnoreCase);

        private static Regex BuildPattern(string text)
        {
            var builder = new StringBuilder("^");
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '~' && i + 1 < text.Length && (text[i + 1] == '*' || text[i + 1] == '?' || text[i + 1] == '~'))
                {
                    builder.Append(Regex.Escape(text[i + 1].ToString()));
                    i++;
                }
                else if (c == '*')
                {
                    builder.Append(".*");
                }
                else if (c == '?')
                {
                    builder.Append('.');
                }
                else
                {
                    builder.Append(Regex.Escape(c.ToString()));
                }
            }

            builder.Append('$');
            return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        }
    }
}
