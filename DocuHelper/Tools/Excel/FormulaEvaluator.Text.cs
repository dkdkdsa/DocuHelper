using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DocuHelper.Tools.Excel
{
    internal sealed partial class FormulaEvaluator
    {
        private const int MaxTextLength = 32767;

        private static readonly Regex s_spaces = new(" {2,}", RegexOptions.CultureInvariant);

        private ExcelValue CallText(string name, List<FormulaNode> args)
        {
            switch (name)
            {
                case "LEFT":
                    return Side(args, true);
                case "RIGHT":
                    return Side(args, false);
                case "MID":
                    return Mid(args);
                case "LEN":
                    return TextArgument(args[0], out var text) ?? ExcelValue.FromNumber(text.Length);
                case "TRIM":
                    return TextArgument(args[0], out text) ?? ExcelValue.FromText(s_spaces.Replace(text.Trim(' '), " "));
                case "UPPER":
                    return TextArgument(args[0], out text) ?? ExcelValue.FromText(text.ToUpperInvariant());
                case "LOWER":
                    return TextArgument(args[0], out text) ?? ExcelValue.FromText(text.ToLowerInvariant());
                case "PROPER":
                    return TextArgument(args[0], out text) ?? ExcelValue.FromText(Proper(text));
                case "FIND":
                    return Find(args, false);
                case "SEARCH":
                    return Find(args, true);
                case "SUBSTITUTE":
                    return Substitute(args);
                case "CONCAT":
                    return Join(args, 0, string.Empty, false, true);
                case "CONCATENATE":
                    return Join(args, 0, string.Empty, false, false);
                case "TEXTJOIN":
                    return TextJoin(args);
                case "TEXT":
                    return TextFormat(args);
                case "REPT":
                    return Repeat(args);
                case "VALUE":
                    return ValueOf(Scalar(args[0]));
                case "EXACT":
                    return TextArgument(args[0], out var first) ?? TextArgument(args[1], out var second) ?? ExcelValue.FromBoolean(string.Equals(first, second, StringComparison.Ordinal));
                default:
                    return null;
            }
        }

        private ExcelValue TextArgument(FormulaNode node, out string text)
        {
            var value = node.Kind == FormulaNodeKind.Missing ? ExcelValue.Blank : Scalar(node);
            text = ToText(value);
            return value.IsError ? value : null;
        }

        private ExcelValue Side(List<FormulaNode> args, bool left)
        {
            var problem = TextArgument(args[0], out var text);
            if (problem != null)
                return problem;

            if (!TryInteger(args, 1, 1, out var count, out var error))
                return error;

            if (count < 0)
                return ExcelValue.FromError(ExcelValue.BadValue);

            count = Math.Min(count, text.Length);
            return ExcelValue.FromText(left ? text.Substring(0, count) : text.Substring(text.Length - count));
        }

        private ExcelValue Mid(List<FormulaNode> args)
        {
            var problem = TextArgument(args[0], out var text);
            if (problem != null)
                return problem;

            if (!TryInteger(args, 1, 1, out var start, out var error) || !TryInteger(args, 2, 0, out var count, out error))
                return error;

            if (start < 1 || count < 0)
                return ExcelValue.FromError(ExcelValue.BadValue);

            if (start > text.Length)
                return ExcelValue.FromText(string.Empty);

            return ExcelValue.FromText(text.Substring(start - 1, Math.Min(count, text.Length - start + 1)));
        }

        private ExcelValue Find(List<FormulaNode> args, bool search)
        {
            var problem = TextArgument(args[0], out var find);
            if (problem != null)
                return problem;

            problem = TextArgument(args[1], out var within);
            if (problem != null)
                return problem;

            if (!TryInteger(args, 2, 1, out var start, out var error))
                return error;

            if (start < 1 || start > within.Length + 1)
                return ExcelValue.FromError(ExcelValue.BadValue);

            if (find.Length == 0)
                return ExcelValue.FromNumber(start);

            int index;
            if (search)
            {
                var match = FormulaCriteria.SearchPattern(find).Match(within, start - 1);
                index = match.Success ? match.Index : -1;
            }
            else
            {
                index = within.IndexOf(find, start - 1, StringComparison.Ordinal);
            }

            return index < 0 ? ExcelValue.FromError(ExcelValue.BadValue) : ExcelValue.FromNumber(index + 1);
        }

        private ExcelValue Substitute(List<FormulaNode> args)
        {
            var problem = TextArgument(args[0], out var text);
            if (problem != null)
                return problem;

            problem = TextArgument(args[1], out var old);
            if (problem != null)
                return problem;

            problem = TextArgument(args[2], out var replacement);
            if (problem != null)
                return problem;

            if (old.Length == 0)
                return ExcelValue.FromText(text);

            if (args.Count < 4 || args[3].Kind == FormulaNodeKind.Missing)
                return Limit(text.Replace(old, replacement, StringComparison.Ordinal));

            if (!TryInteger(args, 3, 1, out var instance, out var error))
                return error;

            if (instance < 1)
                return ExcelValue.FromError(ExcelValue.BadValue);

            var index = -1;
            for (var n = 0; n < instance; n++)
            {
                index = text.IndexOf(old, index + 1, StringComparison.Ordinal);
                if (index < 0)
                    return ExcelValue.FromText(text);
            }

            return Limit(text.Substring(0, index) + replacement + text.Substring(index + old.Length));
        }

        private ExcelValue TextJoin(List<FormulaNode> args)
        {
            var problem = TextArgument(args[0], out var delimiter);
            if (problem != null)
                return problem;

            if (!TryBoolean(Scalar(args[1]), out var ignoreEmpty, out var error))
                return error;

            return Join(args, 2, delimiter, ignoreEmpty, true);
        }

        private ExcelValue Join(List<FormulaNode> args, int from, string delimiter, bool ignoreEmpty, bool ranges)
        {
            var parts = new List<string>();
            for (var i = from; i < args.Count; i++)
            {
                var arg = args[i];
                if (arg.Kind == FormulaNodeKind.Missing)
                {
                    if (!ignoreEmpty)
                        parts.Add(string.Empty);

                    continue;
                }

                var operand = ranges ? Eval(arg) : FormulaOperand.Of(Scalar(arg));
                var values = operand.IsRange ? Values(operand) : new[] { operand.Value };
                foreach (var value in values)
                {
                    if (value.IsError)
                        return value;

                    var text = ToText(value);
                    if (ignoreEmpty && text.Length == 0)
                        continue;

                    parts.Add(text);
                }
            }

            return Limit(string.Join(delimiter, parts));
        }

        private ExcelValue TextFormat(List<FormulaNode> args)
        {
            var value = Scalar(args[0]);
            if (value.IsError)
                return value;

            var problem = TextArgument(args[1], out var format);
            if (problem != null)
                return problem;

            double number;
            switch (value.Kind)
            {
                case ExcelValueKind.Number:
                    number = value.Number;
                    break;
                case ExcelValueKind.Blank:
                    number = 0;
                    break;
                case ExcelValueKind.Text when TypedInput.TryParseNumber(value.Text, out var parsed, out _):
                    number = parsed;
                    break;
                default:
                    return ExcelValue.FromText(ToText(value));
            }

            try
            {
                return ExcelValue.FromText(ExcelNumberFormat.Format(number, format));
            }
            catch (FormatException)
            {
                return ExcelValue.FromError(ExcelValue.BadValue);
            }
        }

        private ExcelValue Repeat(List<FormulaNode> args)
        {
            var problem = TextArgument(args[0], out var text);
            if (problem != null)
                return problem;

            if (!TryInteger(args, 1, 0, out var count, out var error))
                return error;

            if (count < 0 || (long)text.Length * count > MaxTextLength)
                return ExcelValue.FromError(ExcelValue.BadValue);

            return ExcelValue.FromText(string.Concat(Enumerable.Repeat(text, count)));
        }

        private static ExcelValue ValueOf(ExcelValue value) => value.Kind switch
        {
            ExcelValueKind.Number => value,
            ExcelValueKind.Blank => ExcelValue.Zero,
            ExcelValueKind.Error => value,
            ExcelValueKind.Text when TypedInput.TryParseNumber(value.Text.Trim(), out var number, out _) => ExcelValue.FromNumber(number),
            _ => ExcelValue.FromError(ExcelValue.BadValue),
        };

        private static ExcelValue Limit(string text) =>
            text.Length > MaxTextLength ? ExcelValue.FromError(ExcelValue.BadValue) : ExcelValue.FromText(text);

        private static string Proper(string text)
        {
            var builder = new StringBuilder(text.Length);
            var previousLetter = false;
            foreach (var c in text)
            {
                builder.Append(previousLetter ? char.ToLower(c, CultureInfo.InvariantCulture) : char.ToUpper(c, CultureInfo.InvariantCulture));
                previousLetter = char.IsLetter(c);
            }

            return builder.ToString();
        }
    }
}
