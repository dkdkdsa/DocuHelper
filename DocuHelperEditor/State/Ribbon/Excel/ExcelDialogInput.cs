using System;
using System.Globalization;
using System.Text.RegularExpressions;
using DocuHelper.Tools.Excel;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public static class ExcelDialogInput
    {
        public const int MaxFormulaLength = 255;

        private static readonly Regex s_time = new(@"^(\d{1,2}):(\d{2})(?::(\d{2}))?$", RegexOptions.CultureInvariant);

        private static readonly string[] s_dateFormats = { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd" };

        public static string Required(string text, string label) =>
            string.IsNullOrWhiteSpace(text) ? label + "이(가) 비어 있어요." : null;

        public static string Range(string text, string label)
        {
            var problem = Required(text, label);
            if (problem != null)
                return problem;

            return CellRange.TryParse(text, CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1, out _) ? null : label + " 주소를 확인해 주세요. 예: A1:C10";
        }

        public static string Cell(string text, string label)
        {
            var problem = Required(text, label);
            if (problem != null)
                return problem;

            return CellAddress.TryParse(text, out _) ? null : label + " 주소를 확인해 주세요. 예: B5";
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
            if (TryNumber(value, out _) || TryDate(value, out _) || CellAddress.TryParse(value, out _))
                return null;

            return label + "에는 숫자, 날짜(예: 2026-06-30), 칸 주소(예: B1) 중 하나를 넣어 주세요.";
        }

        public static string Date(string text, string label)
        {
            var problem = Required(text, label);
            if (problem != null)
                return problem;

            return TryDate(text.Trim(), out _) || CellAddress.TryParse(text, out _)
                ? null
                : label + ": 2026-06-30 같은 날짜나 칸 주소로 넣어 주세요.";
        }

        public static string Time(string text, string label)
        {
            var problem = Required(text, label);
            if (problem != null)
                return problem;

            var value = text.Trim();
            if (CellAddress.TryParse(value, out _) || value.StartsWith("=", StringComparison.Ordinal) || IsTimeOfDay(value))
                return null;

            return TryNumber(value, out var number) && number >= 0 && number < 1 ? null : label + ": 09:00 같은 시각으로 넣어 주세요.";
        }

        public static string NumberOrReference(string text, string label)
        {
            var problem = Required(text, label);
            if (problem != null)
                return problem;

            var value = text.Trim();
            return CellAddress.TryParse(value, out _) || value.StartsWith("=", StringComparison.Ordinal) ? null : Number(text, label);
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

        public static bool LooksNumeric(string text) => !string.IsNullOrEmpty(text) && (TryNumber(text, out _) || TryDate(text, out _));

        private static bool IsTimeOfDay(string text)
        {
            var match = s_time.Match(text);
            if (!match.Success)
                return false;

            var hours = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var seconds = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
            return hours < 24 && minutes < 60 && seconds < 60;
        }

        private static bool TryNumber(string text, out double value) =>
            double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

        private static bool TryDate(string text, out DateTime value) =>
            DateTime.TryParseExact(text.Trim(), s_dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }
}
