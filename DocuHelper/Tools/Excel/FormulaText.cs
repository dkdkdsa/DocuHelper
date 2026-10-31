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

        public static string ForStorage(string formula) => RenameFunctions(formula, FormulaFunctions.StorageName);

        public static string ForDisplay(string formula) => RenameFunctions(formula, FormulaFunctions.Normalize);

        public static string Rewrite(string formula, Func<FormulaReference, FormulaReference> map, out bool broken)
        {
            var any = false;
            var text = Rewrite(formula, reference =>
            {
                var mapped = map(reference);
                if (mapped != null)
                    return mapped.Render();

                any = true;
                return reference.RenderPrefix() + ExcelValue.BadReference;
            }, FunctionText);
            broken = any;
            return text;
        }

        public static string Replace(string formula, Func<FormulaReference, string> replace) =>
            Rewrite(formula, reference => replace(reference) ?? reference.Render(), FunctionText);

        private static string RenameFunctions(string formula, Func<string, string> rename)
        {
            if (string.IsNullOrEmpty(formula))
                return formula;

            try
            {
                return Rewrite(formula, reference => reference.Render(), rename);
            }
            catch (FormulaException)
            {
                return formula;
            }
        }

        private static string FunctionText(string name)
        {
            var upper = name.ToUpperInvariant();
            foreach (var prefix in new[] { "_XLFN._XLWS.", "_XLFN.", "_XLWS." })
            {
                if (upper.StartsWith(prefix, StringComparison.Ordinal))
                    return prefix.ToLowerInvariant() + upper.Substring(prefix.Length);
            }

            return upper;
        }

        private static string Rewrite(string formula, Func<FormulaReference, string> reference, Func<string, string> functionName)
        {
            var tokens = FormulaLexer.Tokenize(formula);
            var builder = new StringBuilder();
            var last = 0;
            foreach (var token in tokens)
            {
                builder.Append(formula, last, token.Start - last);
                switch (token.Kind)
                {
                    case FormulaTokenKind.Reference:
                        builder.Append(reference(token.Reference));
                        break;
                    case FormulaTokenKind.Function:
                        builder.Append(functionName(token.Text));
                        break;
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
