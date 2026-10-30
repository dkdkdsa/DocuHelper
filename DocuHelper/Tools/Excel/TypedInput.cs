using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DocuHelper.Tools.Excel
{
    public sealed class TypedInput
    {
        public const string ThousandsHint = "thousands";
        public const string PercentHint = "percent";

        private static readonly Regex s_plain = new(@"^[+-]?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?$", RegexOptions.CultureInvariant);
        private static readonly Regex s_grouped = new(@"^[+-]?\d{1,3}(,\d{3})+(\.\d+)?$", RegexOptions.CultureInvariant);
        private static readonly Regex s_date = new(@"^(\d{4})\s*[-/.]\s*(\d{1,2})\s*[-/.]\s*(\d{1,2})\.?$", RegexOptions.CultureInvariant);

        private TypedInput(TypedInputKind kind, double number, string text, string formatHint)
        {
            Kind = kind;
            Number = number;
            Text = text;
            FormatHint = formatHint;
        }

        public TypedInputKind Kind { get; }

        public double Number { get; }

        public string Text { get; }

        public string FormatHint { get; }

        public static TypedInput Parse(string input)
        {
            if (string.IsNullOrEmpty(input))
                return new TypedInput(TypedInputKind.Clear, 0, string.Empty, null);

            if (input[0] == '\'')
                return new TypedInput(TypedInputKind.Text, 0, input.Substring(1), null);

            if (input.Length > 1 && input[0] == '=')
                return new TypedInput(TypedInputKind.Formula, 0, input.Substring(1), null);

            var trimmed = input.Trim();
            if (string.Equals(trimmed, "TRUE", StringComparison.OrdinalIgnoreCase))
                return new TypedInput(TypedInputKind.Boolean, 1, null, null);

            if (string.Equals(trimmed, "FALSE", StringComparison.OrdinalIgnoreCase))
                return new TypedInput(TypedInputKind.Boolean, 0, null, null);

            if (TryParseNumber(trimmed, out var number, out var hint))
                return new TypedInput(TypedInputKind.Number, number, null, hint);

            return new TypedInput(TypedInputKind.Text, 0, input, null);
        }

        public static TypedInput FromText(string text) => new(TypedInputKind.Text, 0, text ?? string.Empty, null);

        public static TypedInput FromNumber(double number, string formatHint = null) => new(TypedInputKind.Number, number, null, formatHint);

        public static TypedInput FromBoolean(bool value) => new(TypedInputKind.Boolean, value ? 1 : 0, null, null);

        public static TypedInput FromFormula(string formula) => new(TypedInputKind.Formula, 0, formula, null);

        public static bool TryParseNumber(string text, out double number, out string hint)
        {
            number = 0;
            hint = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var value = text.Trim();
            if (value.EndsWith("%", StringComparison.Ordinal))
            {
                var core = value.Substring(0, value.Length - 1).TrimEnd();
                if (!TryParsePlain(core, out var percent))
                    return false;

                number = percent / 100;
                hint = PercentHint;
                return true;
            }

            var date = s_date.Match(value);
            if (date.Success)
            {
                var year = int.Parse(date.Groups[1].Value, CultureInfo.InvariantCulture);
                var month = int.Parse(date.Groups[2].Value, CultureInfo.InvariantCulture);
                var day = int.Parse(date.Groups[3].Value, CultureInfo.InvariantCulture);
                if (year < 1900 || year > 9999 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
                    return false;

                number = ExcelNumberFormat.ToSerial(new DateTime(year, month, day));
                hint = ExcelNumberFormat.DateCode;
                return true;
            }

            if (s_grouped.IsMatch(value))
            {
                number = double.Parse(value.Replace(",", string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture);
                hint = ThousandsHint;
                return true;
            }

            return TryParsePlain(value, out number);
        }

        private static bool TryParsePlain(string text, out double number)
        {
            number = 0;
            if (!s_plain.IsMatch(text) && !s_grouped.IsMatch(text))
                return false;

            return double.TryParse(text.Replace(",", string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                && !double.IsInfinity(number);
        }
    }
}
