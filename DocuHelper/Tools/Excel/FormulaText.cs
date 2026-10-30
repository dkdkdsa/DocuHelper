using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocuHelper.Tools.Excel
{
    public static class FormulaText
    {
        public static string Strip(string formula)
        {
            var text = (formula ?? string.Empty).Trim();
            return text.StartsWith("=", StringComparison.Ordinal) ? text.Substring(1).TrimStart() : text;
        }

        public static string Rewrite(string formula, Func<FormulaReference, FormulaReference> map, out bool broken)
        {
            broken = false;
            var tokens = FormulaLexer.Tokenize(formula);
            var builder = new StringBuilder();
            var last = 0;
            foreach (var token in tokens)
            {
                builder.Append(formula, last, token.Start - last);
                switch (token.Kind)
                {
                    case FormulaTokenKind.Reference:
                        var mapped = map(token.Reference);
                        if (mapped == null)
                        {
                            broken = true;
                            builder.Append(token.Reference.RenderPrefix()).Append(ExcelValue.BadReference);
                        }
                        else
                        {
                            builder.Append(mapped.Render());
                        }

                        break;
                    case FormulaTokenKind.Function:
                    case FormulaTokenKind.Boolean:
                        builder.Append(token.Text.ToUpperInvariant());
                        break;
                    case FormulaTokenKind.Error:
                        builder.Append(token.Text.ToUpperInvariant());
                        break;
                    default:
                        builder.Append(token.Text);
                        break;
                }

                last = token.End;
            }

            builder.Append(formula, last, formula.Length - last);
            return builder.ToString();
        }

        public static string Normalize(string formula) => Rewrite(formula, r => r, out _);

        public static bool TryShift(string formula, int rowDelta, int columnDelta, out string shifted)
        {
            shifted = Rewrite(formula, r => r.Shift(rowDelta, columnDelta), out var broken);
            return !broken;
        }

        public static string Shift(string formula, int rowDelta, int columnDelta)
        {
            try
            {
                return Rewrite(formula, r => r.Shift(rowDelta, columnDelta), out _);
            }
            catch (FormulaException)
            {
                return formula;
            }
        }

        public static IReadOnlyList<FormulaReference> References(string formula)
        {
            try
            {
                return FormulaLexer.Tokenize(Strip(formula))
                    .Where(t => t.Kind == FormulaTokenKind.Reference)
                    .Select(t => t.Reference)
                    .ToList();
            }
            catch (FormulaException)
            {
                return Array.Empty<FormulaReference>();
            }
        }
    }
}
