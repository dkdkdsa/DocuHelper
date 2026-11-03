using System;
using System.Globalization;

namespace DocuHelper.Dash.Runtime
{
    public static class DashFormats
    {
        private static readonly CultureInfo s_korean = CultureInfo.GetCultureInfo("ko-KR");

        public static string Display(object value, string format)
        {
            if (value == null)
                return string.Empty;

            if (value is DashError error)
                return error.Code;

            var (kind, argument) = Split(format);
            switch (kind)
            {
                case "text":
                    return DashCells.Text(value);
                case "day":
                    return Day(value, argument);
                case "date":
                    return value is DateTime full ? full.ToString("yyyy년 M월 d일", s_korean) : DashCells.Text(value);
            }

            if (value is bool flag)
                return flag ? "켜짐" : "꺼짐";

            if (value is DateTime date)
                return $"{date.Month}월 {date.Day}일";

            if (value is string text)
                return text;

            var number = DashCells.Number(value);
            if (!number.HasValue)
                return DashCells.Text(value);

            var n = number.Value;
            return kind switch
            {
                "man" => Man(n),
                "won" => Group(n) + "원",
                "gun" => Group(n) + "건",
                "pct" => Percent(n, argument),
                "num" or "" => Group(n),
                _ when IsPattern(format) => n.ToString(format, s_korean),
                _ => Group(n),
            };
        }

        public static string DisplayRange(double from, double to, string format)
        {
            var (kind, argument) = Split(format);
            if (kind == "day")
            {
                var first = Day(from, argument);
                if (Math.Abs(from - to) < 1e-9)
                    return first;

                return $"{first} ~ {Day(to, null)}";
            }

            var start = Display(from, format);
            return Math.Abs(from - to) < 1e-9 ? start : $"{start} ~ {Display(to, format)}";
        }

        public static string Group(double number) =>
            Math.Abs(number - Math.Round(number)) < 1e-9
                ? Math.Round(number).ToString("#,##0", CultureInfo.InvariantCulture)
                : number.ToString("#,##0.##", CultureInfo.InvariantCulture);

        private static string Man(double number)
        {
            if (Math.Abs(number) < 10000)
                return Group(number) + "원";

            var man = number / 10000;
            var text = Math.Abs(man) >= 100
                ? Math.Round(man).ToString("#,##0", CultureInfo.InvariantCulture)
                : man.ToString("#,##0.#", CultureInfo.InvariantCulture);
            return text + "만 원";
        }

        private static string Percent(double ratio, string digits)
        {
            var places = int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? Math.Clamp(parsed, 0, 4) : 0;
            var pattern = places == 0 ? "#,##0" : "#,##0." + new string('0', places);
            return (ratio * 100).ToString(pattern, CultureInfo.InvariantCulture) + "%";
        }

        private static string Day(object value, string month)
        {
            if (value is DateTime date)
                return $"{date.Month}월 {date.Day}일";

            var day = DashCells.Day(value);
            if (!day.HasValue)
                return DashCells.Text(value);

            return string.IsNullOrWhiteSpace(month) ? $"{day.Value}일" : $"{month.Trim()}월 {day.Value}일";
        }

        private static (string Kind, string Argument) Split(string format)
        {
            var code = (format ?? string.Empty).Trim();
            var colon = code.IndexOf(':');
            return colon < 0
                ? (code.ToLowerInvariant(), null)
                : (code.Substring(0, colon).Trim().ToLowerInvariant(), code.Substring(colon + 1).Trim());
        }

        private static bool IsPattern(string format) => format != null && (format.Contains('0') || format.Contains('#'));
    }
}
