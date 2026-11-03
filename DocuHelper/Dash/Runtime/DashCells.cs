using System;
using System.Globalization;
using System.Text.RegularExpressions;
using DocuHelper.Tools.Excel;

namespace DocuHelper.Dash.Runtime
{
    public static class DashCells
    {
        private const double LastOaDate = 2958466;

        private static readonly CultureInfo s_korean = CultureInfo.GetCultureInfo("ko-KR");
        private static readonly string[] s_dateFormats = { "yyyy-MM-dd", "yyyy-M-d", "yyyy.MM.dd", "yyyy.M.d", "yyyy/MM/dd", "yyyy/M/d", "yyyy. M. d.", "yyyy. M. d" };
        private static readonly Regex s_koreanDate = new(@"^(\d{4})년\s*(\d{1,2})월\s*(\d{1,2})일$", RegexOptions.CultureInvariant);

        public static double? Number(object value) => value switch
        {
            null => null,
            double number => number,
            int number => number,
            long number => number,
            decimal number => (double)number,
            DateTime date => date.ToOADate(),
            string text => ParseNumber(text),
            _ => null,
        };

        public static double? ParseNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var value = text.Trim();
            var percent = value.EndsWith('%');
            if (percent)
                value = value.Substring(0, value.Length - 1);

            value = value.Replace(",", string.Empty).Replace("₩", string.Empty).Trim();
            if (value.EndsWith('원'))
                value = value.Substring(0, value.Length - 1).Trim();

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return null;

            return percent ? number / 100 : number;
        }

        public static DateTime? ParseDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var value = text.Trim();
            if (DateTime.TryParseExact(value, s_dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date;

            var match = s_koreanDate.Match(value);
            if (match.Success
                && int.TryParse(match.Groups[1].Value, out var year)
                && int.TryParse(match.Groups[2].Value, out var month)
                && int.TryParse(match.Groups[3].Value, out var day)
                && month is >= 1 and <= 12
                && day >= 1 && day <= DateTime.DaysInMonth(year, month))
                return new DateTime(year, month, day);

            return null;
        }

        public static object Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var value = text.Trim();
            var date = ParseDate(value);
            if (date.HasValue)
                return date.Value;

            var number = ParseNumber(value);
            return number.HasValue ? number.Value : value;
        }

        public static object FromExcel(ExcelValue value, bool isDate)
        {
            if (value == null || value.IsBlank)
                return null;

            if (value.IsNumber)
                return isDate && value.Number > 0 && value.Number < LastOaDate ? DateTime.FromOADate(value.Number) : value.Number;

            if (value.IsBoolean)
                return value.Boolean;

            if (value.IsError)
                return new DashError(value.Text);

            return value.Text;
        }

        public static string Text(object value) => value switch
        {
            null => string.Empty,
            string text => text,
            double number => ExcelValue.Invariant(number),
            DateTime date => date.TimeOfDay == TimeSpan.Zero
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            bool flag => flag ? "TRUE" : "FALSE",
            DashError error => error.Code,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };

        public static bool Same(object a, object b)
        {
            if (a == null || b == null)
                return a == null && b == null;

            if (a is string || b is string)
                return string.Equals(Text(a).Trim(), Text(b).Trim(), StringComparison.Ordinal);

            var x = Number(a);
            var y = Number(b);
            return x.HasValue && y.HasValue ? Math.Abs(x.Value - y.Value) < 1e-9 : Equals(a, b);
        }

        public static int Compare(object a, object b)
        {
            if (a == null || b == null)
                return a == null ? (b == null ? 0 : 1) : -1;

            var x = a is string ? null : Number(a);
            var y = b is string ? null : Number(b);
            if (x.HasValue && y.HasValue)
                return x.Value.CompareTo(y.Value);

            if (x.HasValue != y.HasValue)
                return x.HasValue ? -1 : 1;

            return string.Compare(Text(a), Text(b), s_korean, CompareOptions.None);
        }

        public static int? Day(object value)
        {
            switch (value)
            {
                case DateTime date:
                    return date.Day;
                case double number when number >= 1 && number < 32:
                    return (int)Math.Floor(number);
                case double number when number >= 32 && number < LastOaDate:
                    return DateTime.FromOADate(number).Day;
                case string text:
                    var parsed = ParseDate(text);
                    if (parsed.HasValue)
                        return parsed.Value.Day;

                    var day = ParseNumber(text);
                    return day.HasValue && day.Value >= 1 && day.Value < 32 ? (int)Math.Floor(day.Value) : null;
                default:
                    return null;
            }
        }

        public static bool Truthy(object value) => value switch
        {
            null => false,
            bool flag => flag,
            double number => number != 0,
            string text => text.Length > 0 && text != "0" && !string.Equals(text, "FALSE", StringComparison.OrdinalIgnoreCase),
            DashError => false,
            _ => true,
        };
    }
}
