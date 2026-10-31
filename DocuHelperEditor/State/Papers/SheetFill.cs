using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DocuHelper.Tools.Excel;

namespace DocuHelperEditor.State.Papers
{
    public static class SheetFill
    {
        private const string DateFormat = "yyyy-MM-dd";

        private static readonly Regex s_numbered = new(@"^(.*?)(\d+)(\D*)$", RegexOptions.CultureInvariant);

        private static readonly string[][] s_lists =
        {
            new[] { "월", "화", "수", "목", "금", "토", "일" },
            new[] { "월요일", "화요일", "수요일", "목요일", "금요일", "토요일", "일요일" },
            new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" },
            new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" },
            new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" },
            new[] { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" },
            new[] { "갑", "을", "병", "정", "무", "기", "경", "신", "임", "계" },
        };

        public static List<string> Extend(IReadOnlyList<SheetCell> source, int count, bool backward, bool vertical, bool series)
        {
            var values = source.Select(c => c?.EditText ?? string.Empty).ToList();
            if (series && values.All(v => v.Length > 0) && source.All(c => string.IsNullOrEmpty(c?.Formula)))
            {
                var continued = source.All(c => c != null && c.IsNumber)
                    ? Numbers(source, values, count, backward) ?? Dates(values, count, backward)
                    : Numbered(values, count, backward) ?? Listed(values, count, backward);
                if (continued != null)
                    return continued;
            }

            return Repeat(source, values, count, backward, vertical);
        }

        private static List<string> Repeat(IReadOnlyList<SheetCell> source, List<string> values, int count, bool backward, bool vertical)
        {
            var result = new List<string>();
            var n = values.Count;
            for (var k = 0; k < count; k++)
            {
                var index = backward ? n - 1 - (k % n) : k % n;
                var offset = backward ? -(k + 1) - index : n + k - index;
                var formula = source[index]?.Formula;
                if (string.IsNullOrEmpty(formula))
                {
                    result.Add(values[index]);
                    continue;
                }

                var shifted = FormulaText.Shift(FormulaText.Strip(formula), vertical ? offset : 0, vertical ? 0 : offset);
                result.Add("=" + shifted);
            }

            return result;
        }

        private static List<string> Numbers(IReadOnlyList<SheetCell> source, List<string> values, int count, bool backward)
        {
            var numbers = new List<double>();
            for (var i = 0; i < values.Count; i++)
            {
                if (source[i] == null || !source[i].IsNumber || !double.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    return null;

                numbers.Add(number);
            }

            if (numbers.Count == 1)
                return Enumerable.Repeat(values[0], count).ToList();

            var step = (numbers[^1] - numbers[0]) / (numbers.Count - 1);
            var integers = numbers.All(IsWhole) && IsWhole(step);
            var result = new List<string>();
            for (var k = 1; k <= count; k++)
            {
                var value = backward ? numbers[0] - (step * k) : numbers[^1] + (step * k);
                result.Add(integers ? Math.Round(value).ToString(CultureInfo.InvariantCulture) : Math.Round(value, 10).ToString("G15", CultureInfo.InvariantCulture));
            }

            return result;
        }

        private static List<string> Dates(List<string> values, int count, bool backward)
        {
            var dates = new List<DateTime>();
            foreach (var value in values)
            {
                if (!DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    return null;

                dates.Add(date);
            }

            var days = dates.Count == 1 ? 1 : (dates[^1] - dates[0]).TotalDays / (dates.Count - 1);
            if (!IsWhole(days) || days == 0)
                return null;

            var result = new List<string>();
            for (var k = 1; k <= count; k++)
            {
                var date = backward ? dates[0].AddDays(-days * k) : dates[^1].AddDays(days * k);
                result.Add(date.ToString(DateFormat, CultureInfo.InvariantCulture));
            }

            return result;
        }

        private static List<string> Numbered(List<string> values, int count, bool backward)
        {
            var matches = values.Select(v => s_numbered.Match(v)).ToList();
            if (matches.Any(m => !m.Success))
                return null;

            var prefix = matches[0].Groups[1].Value;
            var suffix = matches[0].Groups[3].Value;
            if (matches.Any(m => m.Groups[1].Value != prefix || m.Groups[3].Value != suffix))
                return null;

            var numbers = matches.Select(m => long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)).ToList();
            var width = matches[0].Groups[2].Value.Length;
            var padded = matches[0].Groups[2].Value.StartsWith("0", StringComparison.Ordinal) && width > 1;
            long step = 1;
            if (numbers.Count > 1)
            {
                var total = numbers[^1] - numbers[0];
                if (total % (numbers.Count - 1) != 0)
                    return null;

                step = total / (numbers.Count - 1);
            }

            var months = prefix.Length == 0 && suffix == "월" && numbers.All(n => n >= 1 && n <= 12);
            var result = new List<string>();
            for (var k = 1; k <= count; k++)
            {
                var number = backward ? numbers[0] - (step * k) : numbers[^1] + (step * k);
                if (months)
                    number = ((((number - 1) % 12) + 12) % 12) + 1;

                if (number < 0)
                    return null;

                var digits = padded ? number.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0') : number.ToString(CultureInfo.InvariantCulture);
                result.Add(prefix + digits + suffix);
            }

            return result;
        }

        private static List<string> Listed(List<string> values, int count, bool backward)
        {
            foreach (var list in s_lists)
            {
                var indexes = values.Select(v => Array.FindIndex(list, item => string.Equals(item, v.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
                if (indexes.Any(i => i < 0))
                    continue;

                var n = list.Length;
                var step = 1;
                if (indexes.Count > 1)
                {
                    step = ((indexes[1] - indexes[0]) % n + n) % n;
                    var consistent = true;
                    for (var i = 2; i < indexes.Count; i++)
                    {
                        if (((indexes[i] - indexes[i - 1]) % n + n) % n != step)
                            consistent = false;
                    }

                    if (!consistent || step == 0)
                        continue;
                }

                var result = new List<string>();
                for (var k = 1; k <= count; k++)
                {
                    var index = backward ? indexes[0] - (step * k) : indexes[^1] + (step * k);
                    result.Add(list[((index % n) + n) % n]);
                }

                return result;
            }

            return null;
        }

        private static bool IsWhole(double value) => Math.Abs(value - Math.Round(value)) < 1e-9;
    }
}
