using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public static class ExcelDialogInput
    {
        public const int MaxFormulaLength = 255;

        private const string CellPattern = @"\$?[A-Za-z]{1,3}\$?[0-9]{1,7}";

        private static readonly Regex s_cell = new("^" + CellPattern + "$", RegexOptions.CultureInvariant);

        private const string RangeToken = @"\$?[A-Za-z]{1,3}\$?[0-9]{0,7}";

        private static readonly Regex s_range = new(
            "^" + RangeToken + "(:" + RangeToken + ")?(\\s+" + RangeToken + "(:" + RangeToken + ")?)*$",
            RegexOptions.CultureInvariant);

        private static readonly string[] s_dateFormats = { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd" };

        public static string Required(string text, string label) =>
            string.IsNullOrWhiteSpace(text) ? label + "이(가) 비어 있어요." : null;

        public static string Range(string text, string label)
        {
            var problem = Required(text, label);
            if (problem != null)
                return problem;

            return s_range.IsMatch(text.Trim()) ? null : label + " 주소를 확인해 주세요. 예: A1:C10";
        }

        public static string Cell(string text, string label)
        {
            var problem = Required(text, label);
            if (problem != null)
                return problem;

            return s_cell.IsMatch(text.Trim()) ? null : label + " 주소를 확인해 주세요. 예: B5";
        }

        public static string Number(string text, string label)
        {
            var problem = Required(text, label);
            if (problem != null)
                return problem;

            return TryNumber(text, out _) ? null : label + "에는 숫자를 넣어 주세요.";
        }

        public static string Operand(string text, string label)
        {
            var problem = Required(text, label);
            if (problem != null)
                return problem;

            var value = text.Trim();
            if (TryNumber(value, out _) || TryDate(value, out _) || s_cell.IsMatch(value))
                return null;

            return label + "에는 숫자, 날짜(예: 2026-06-30), 칸 주소(예: B1) 중 하나를 넣어 주세요.";
        }

        public static string Date(string text, string label)
        {
            var problem = Required(text, label);
            if (problem != null)
                return problem;

            return TryDate(text.Trim(), out _) || s_cell.IsMatch(text.Trim())
                ? null
                : label + ": 2026-06-30 같은 날짜나 칸 주소로 넣어 주세요.";
        }

        public static string Formula(string text, string label)
        {
            var problem = Required(text, label);
            if (problem != null)
                return problem;

            var formula = text.Trim();
            if (!formula.StartsWith("=", StringComparison.Ordinal))
                return "수식은 =로 시작해야 해요.";

            return formula.Length > MaxFormulaLength ? "수식은 " + MaxFormulaLength.ToString(CultureInfo.InvariantCulture) + "자 이하여야 해요." : null;
        }

        public static string Between(string min, string max, string label)
        {
            if (!TryNumber(min, out var low) || !TryNumber(max, out var high))
                return null;

            return low > high ? label + "의 최소값은 최대값보다 클 수 없어요." : null;
        }

        public static string Percent(int count, bool percent)
        {
            return percent && count > 100 ? "백분율은 100 이하로 넣어 주세요." : null;
        }

        private static bool TryNumber(string text, out double value) =>
            double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static bool TryDate(string text, out DateTime value) =>
            DateTime.TryParseExact(text.Trim(), s_dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }
}
