using System;
using System.Globalization;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public static class SheetValidationCheck
    {
        public static bool Allows(SheetValidation rule, string text, out string message)
        {
            message = null;
            if (rule == null || string.IsNullOrEmpty(text))
                return true;

            var allowed = rule.Type switch
            {
                SheetValidationType.List => rule.Items.Count == 0 || rule.Items.Any(item => string.Equals(item, text, StringComparison.OrdinalIgnoreCase)),
                SheetValidationType.Whole => Number(text, true, out var whole) && Within(rule, whole),
                SheetValidationType.Decimal => Number(text, false, out var value) && Within(rule, value),
                SheetValidationType.TextLength => Within(rule, text.Length),
                _ => true,
            };

            if (allowed)
                return true;

            message = string.IsNullOrEmpty(rule.ErrorMessage) ? Fallback(rule.Type) : rule.ErrorMessage;
            return false;
        }

        private static string Fallback(SheetValidationType type) => type switch
        {
            SheetValidationType.List => "목록에 있는 값만 입력할 수 있어요.",
            SheetValidationType.Whole => "정수만 입력할 수 있어요.",
            SheetValidationType.Decimal => "숫자만 입력할 수 있어요.",
            SheetValidationType.TextLength => "글자 수가 허용 범위를 벗어났어요.",
            _ => "입력한 값이 이 칸의 규칙에 맞지 않아요.",
        };

        private static bool Number(string text, bool whole, out decimal value)
        {
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                return false;

            return !whole || value == decimal.Truncate(value);
        }

        private static bool Within(SheetValidation rule, decimal value)
        {
            if (!TryNumber(rule.Formula1, out var first))
                return true;

            TryNumber(rule.Formula2, out var second);
            return rule.Operator switch
            {
                "notBetween" => value < Math.Min(first, second) || value > Math.Max(first, second),
                "equal" => value == first,
                "notEqual" => value != first,
                "greaterThan" => value > first,
                "lessThan" => value < first,
                "greaterThanOrEqual" => value >= first,
                "lessThanOrEqual" => value <= first,
                _ => value >= Math.Min(first, second) && value <= Math.Max(first, second),
            };
        }

        private static bool TryNumber(string text, out decimal value) =>
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
