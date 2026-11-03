using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocuHelper.Tools.Excel;

namespace DocuHelper.Dash
{
    public static class DashNames
    {
        private const string Item = "item";
        private const string ItemPrefix = "item.";
        private const char ParamMark = '@';

        private static readonly Regex s_template = new("«([^«»]*)»", RegexOptions.CultureInvariant);

        public static IEnumerable<string> InTemplate(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<string>();

            return s_template.Matches(text)
                .Select(match => match.Groups[1].Value.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        public static bool TryParse(string expression, out FormulaNode node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            var text = expression.TrimStart();
            if (text.StartsWith('='))
                text = text.Substring(1);

            return FormulaParser.TryParse(text, out node);
        }

        public static IEnumerable<string> InExpression(string expression)
        {
            if (!TryParse(expression, out var node))
                return Array.Empty<string>();

            return node.Descendants()
                .Where(child => child.Kind == FormulaNodeKind.Name)
                .Select(child => child.Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        public static string ParamOf(string text) =>
            text != null && text.StartsWith(ParamMark) ? text.Substring(1).Trim() : null;

        public static bool IsItem(string name) =>
            name != null && (string.Equals(name, Item, StringComparison.Ordinal) || name.StartsWith(ItemPrefix, StringComparison.Ordinal));

        public static string ItemColumn(string name) =>
            name != null && name.StartsWith(ItemPrefix, StringComparison.Ordinal) && name.Length > ItemPrefix.Length
                ? name.Substring(ItemPrefix.Length)
                : null;
    }
}
