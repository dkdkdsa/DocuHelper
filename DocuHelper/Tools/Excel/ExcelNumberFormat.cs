using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DocuHelper.Tools.Excel
{
    public static class ExcelNumberFormat
    {
        public const uint GeneralId = 0;
        public const uint ThousandsId = 3;
        public const uint PercentId = 9;
        public const uint TextId = 49;
        public const string DateCode = "yyyy-mm-dd";

        private const double MaxSerial = 2958466;

        private static readonly ConcurrentDictionary<string, List<NumberFormatSection>> s_cache = new(StringComparer.Ordinal);
        private static readonly string[] s_koreanDays = { "일", "월", "화", "수", "목", "금", "토" };

        public static string BuiltInCode(uint id) => id switch
        {
            0 => "General",
            1 => "0",
            2 => "0.00",
            3 => "#,##0",
            4 => "#,##0.00",
            5 => "\"₩\"#,##0;\"₩\"\\-#,##0",
            6 => "\"₩\"#,##0;[Red]\"₩\"\\-#,##0",
            7 => "\"₩\"#,##0.00;\"₩\"\\-#,##0.00",
            8 => "\"₩\"#,##0.00;[Red]\"₩\"\\-#,##0.00",
            9 => "0%",
            10 => "0.00%",
            11 => "0.00E+00",
            12 => "# ?/?",
            13 => "# ??/??",
            14 => "yyyy-mm-dd",
            15 => "d-mmm-yy",
            16 => "d-mmm",
            17 => "mmm-yy",
            18 => "h:mm AM/PM",
            19 => "h:mm:ss AM/PM",
            20 => "h:mm",
            21 => "h:mm:ss",
            22 => "yyyy-mm-dd h:mm",
            27 or 31 or 36 or 50 or 57 => "yyyy\"년\" m\"월\" d\"일\"",
            28 or 29 or 51 or 54 or 58 => "m\"월\" d\"일\"",
            30 => "mm-dd-yy",
            32 => "h\"시\" mm\"분\"",
            33 => "h\"시\" mm\"분\" ss\"초\"",
            34 or 35 or 52 or 53 or 55 or 56 => "yyyy-mm-dd",
            37 => "#,##0 ;(#,##0)",
            38 => "#,##0 ;[Red](#,##0)",
            39 => "#,##0.00;(#,##0.00)",
            40 => "#,##0.00;[Red](#,##0.00)",
            45 => "mm:ss",
            46 => "[h]:mm:ss",
            47 => "mmss.0",
            48 => "##0.0E+0",
            49 => "@",
            _ => "General",
        };

        public static uint? BuiltInId(string code) => (code ?? string.Empty).Trim() switch
        {
            "General" or "general" => 0,
            "0" => 1,
            "0.00" => 2,
            "#,##0" => 3,
            "#,##0.00" => 4,
            "0%" => 9,
            "0.00%" => 10,
            "0.00E+00" => 11,
            "@" => 49,
            _ => null,
        };

        public static bool IsGeneral(string code) =>
            string.IsNullOrWhiteSpace(code) || string.Equals(code.Trim(), "General", StringComparison.OrdinalIgnoreCase);

        public static bool IsText(string code) => code != null && code.Trim() == "@";

        public static bool IsDate(string code) => !IsGeneral(code) && Sections(code)[0].IsDate;

        public static bool IsPercent(string code) => !IsGeneral(code) && Sections(code)[0].HasPercent;

        public static bool IsThousands(string code) => !IsGeneral(code) && Sections(code)[0].HasThousands;

        public static string Format(double value, string code)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return ExcelValue.BadNumber;

            if (IsGeneral(code))
                return FormatGeneral(value);

            var sections = Sections(code);
            var section = sections[0];
            var negativeSection = false;
            if (value < 0 && sections.Count >= 2)
            {
                section = sections[1];
                negativeSection = true;
            }
            else if (value == 0 && sections.Count >= 3)
            {
                section = sections[2];
            }

            if (section.IsDate)
                return FormatDate(value, section);

            if (!section.HasDigits)
            {
                if (section.HasGeneral || section.Tokens.All(t => t.Kind == NumberFormatSection.TextPart || (t.Kind == NumberFormatSection.Literal && t.Text.Trim().Length == 0)))
                    return Compose(section, negativeSection ? FormatGeneral(Math.Abs(value)) : FormatGeneral(value), -1, -1);

                return Compose(section, string.Empty, -1, -1);
            }

            if (section.Tokens.Any(t => t.Kind == NumberFormatSection.Literal && t.Text == "/"))
                return FormatGeneral(value);

            var text = FormatNumber(Math.Abs(value), section);
            if (value < 0 && !negativeSection && text.Any(c => c >= '1' && c <= '9'))
                text = "-" + text;

            return text;
        }

        public static string FormatGeneral(double value)
        {
            if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
                return "0";

            var abs = Math.Abs(value);
            if (abs >= 1e11)
                return value.ToString("0.#####E+00", CultureInfo.InvariantCulture);

            if (abs < 1e-9)
                return value.ToString("0.#####E-00", CultureInfo.InvariantCulture);

            if (Math.Floor(value) == value)
                return value.ToString("0", CultureInfo.InvariantCulture);

            return value.ToString("G10", CultureInfo.InvariantCulture);
        }

        public static string EditText(double value, string code)
        {
            if (!IsGeneral(code))
            {
                var section = Sections(code)[0];
                if (section.IsDate && value >= 1 && value < MaxSerial && Math.Floor(value) == value)
                    return ToDateTime(value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                if (section.HasPercent)
                    return ExcelValue.Invariant(value * 100) + "%";
            }

            return ExcelValue.Invariant(value);
        }

        public static DateTime ToDateTime(double serial)
        {
            var days = Math.Floor(serial);
            var origin = serial < 61 ? new DateTime(1899, 12, 31) : new DateTime(1899, 12, 30);
            var seconds = Math.Round((serial - days) * 86400, MidpointRounding.AwayFromZero);
            return origin.AddDays(days).AddSeconds(seconds);
        }

        public static double ToSerial(DateTime date)
        {
            var serial = date.ToOADate();
            return serial < 61 ? serial - 1 : serial;
        }

        private static List<NumberFormatSection> Sections(string code) =>
            s_cache.GetOrAdd(code, key => NumberFormatSection.Split(key).Select(NumberFormatSection.Parse).ToList());

        private static string FormatNumber(double abs, NumberFormatSection section)
        {
            var tokens = section.Tokens;
            var first = tokens.FindIndex(t => t.Kind == NumberFormatSection.Digit);
            var last = tokens.FindLastIndex(t => t.Kind == NumberFormatSection.Digit);
            var exponent = tokens.FindIndex(t => t.Kind == NumberFormatSection.Exponent);
            var mantissaEnd = exponent >= 0 ? exponent : last + 1;
            var point = tokens.FindIndex(first, Math.Max(0, mantissaEnd - first), t => t.Kind == NumberFormatSection.Point);

            var integerDigits = new List<char>();
            var fractionDigits = new List<char>();
            var lastInteger = -1;
            for (var i = first; i < mantissaEnd; i++)
            {
                if (tokens[i].Kind != NumberFormatSection.Digit)
                    continue;

                if (point >= 0 && i > point)
                {
                    fractionDigits.Add(tokens[i].Text[0]);
                }
                else
                {
                    integerDigits.Add(tokens[i].Text[0]);
                    lastInteger = i;
                }
            }

            var end = last;
            var scale = 0;
            for (var i = lastInteger + 1; i < tokens.Count && tokens[i].Kind == NumberFormatSection.Comma; i++)
            {
                scale++;
                end = Math.Max(end, i);
            }

            while (end + 1 < tokens.Count && tokens[end + 1].Kind == NumberFormatSection.Comma)
                end++;

            var percents = tokens.Count(t => t.Kind == NumberFormatSection.Percent);
            var value = abs * Math.Pow(100, percents) / Math.Pow(1000, scale);

            string number;
            if (exponent >= 0)
            {
                var exponentDigits = tokens.Skip(exponent + 1).Count(t => t.Kind == NumberFormatSection.Digit);
                number = FormatScientific(value, fractionDigits.Count, exponentDigits, tokens[exponent].Text);
            }
            else
            {
                number = FormatFixed(value, integerDigits, fractionDigits, section.HasThousands, point >= 0);
            }

            return Compose(section, number, first, end);
        }

        private static string FormatFixed(double value, List<char> integerDigits, List<char> fractionDigits, bool thousands, bool hasPoint)
        {
            var decimals = Math.Min(fractionDigits.Count, 20);
            if (value >= 7.9e27)
                return FormatGeneral(value);

            var rounded = Math.Round((decimal)value, decimals, MidpointRounding.AwayFromZero);
            var text = rounded.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            var dot = text.IndexOf('.');
            var integerPart = dot < 0 ? text : text.Substring(0, dot);
            var fraction = dot < 0 ? string.Empty : text.Substring(dot + 1);

            var minDecimals = fractionDigits.LastIndexOf('0') + 1;
            fraction = fraction.TrimEnd('0');
            if (fraction.Length < minDecimals)
                fraction = fraction.PadRight(minDecimals, '0');

            var minInteger = integerDigits.Count(c => c == '0');
            if (integerPart == "0" && minInteger == 0)
                integerPart = string.Empty;

            if (integerPart.Length < minInteger)
                integerPart = integerPart.PadLeft(minInteger, '0');

            if (thousands && integerPart.Length > 3)
                integerPart = Group(integerPart);

            return hasPoint ? integerPart + "." + fraction : integerPart;
        }

        private static string FormatScientific(double value, int decimals, int exponentDigits, string sign)
        {
            var exponent = value == 0 ? 0 : (int)Math.Floor(Math.Log10(value));
            var mantissa = value == 0 ? 0 : value / Math.Pow(10, exponent);
            mantissa = Math.Round(mantissa, decimals, MidpointRounding.AwayFromZero);
            if (mantissa >= 10)
            {
                mantissa /= 10;
                exponent++;
            }

            var mantissaText = mantissa.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            var exponentText = Math.Abs(exponent).ToString(new string('0', Math.Max(1, exponentDigits)), CultureInfo.InvariantCulture);
            var exponentSign = exponent < 0 ? "-" : sign == "E+" ? "+" : string.Empty;
            return mantissaText + "E" + exponentSign + exponentText;
        }

        private static string Group(string digits)
        {
            var builder = new StringBuilder();
            var lead = digits.Length % 3;
            if (lead > 0)
                builder.Append(digits, 0, lead);

            for (var i = lead; i < digits.Length; i += 3)
            {
                if (builder.Length > 0)
                    builder.Append(',');

                builder.Append(digits, i, 3);
            }

            return builder.ToString();
        }

        private static string Compose(NumberFormatSection section, string number, int first, int end)
        {
            var builder = new StringBuilder();
            var placed = false;
            for (var i = 0; i < section.Tokens.Count; i++)
            {
                var (kind, text) = section.Tokens[i];
                if (first >= 0 && i == first)
                {
                    builder.Append(number);
                    placed = true;
                    i = end;
                    continue;
                }

                switch (kind)
                {
                    case NumberFormatSection.Literal:
                        builder.Append(text);
                        break;
                    case NumberFormatSection.Percent:
                    case NumberFormatSection.Comma:
                    case NumberFormatSection.Point:
                        builder.Append(text);
                        break;
                    case NumberFormatSection.General:
                    case NumberFormatSection.TextPart:
                        if (!placed)
                        {
                            builder.Append(number);
                            placed = true;
                        }

                        break;
                }
            }

            return builder.ToString();
        }

        private static string FormatDate(double value, NumberFormatSection section)
        {
            if (value < 0 || value >= MaxSerial)
                return "########";

            var date = ToDateTime(value);
            var tokens = section.Tokens;
            var twelveHour = tokens.Any(t => t.Kind == NumberFormatSection.AmPm);
            var builder = new StringBuilder();
            for (var i = 0; i < tokens.Count; i++)
            {
                var (kind, text) = tokens[i];
                switch (kind)
                {
                    case NumberFormatSection.DatePart:
                        builder.Append(DatePart(date, value, text, twelveHour, IsMinute(tokens, i)));
                        break;
                    case NumberFormatSection.AmPm:
                        builder.Append(text == "A/P" ? (date.Hour < 12 ? "A" : "P") : (date.Hour < 12 ? "오전" : "오후"));
                        break;
                    case NumberFormatSection.Literal:
                    case NumberFormatSection.Point:
                    case NumberFormatSection.Comma:
                    case NumberFormatSection.Percent:
                    case NumberFormatSection.Digit:
                        builder.Append(text);
                        break;
                }
            }

            return builder.ToString();
        }

        private static string DatePart(DateTime date, double serial, string part, bool twelveHour, bool minute)
        {
            var culture = CultureInfo.InvariantCulture;
            if (part.StartsWith("[", StringComparison.Ordinal))
            {
                return part[1] switch
                {
                    'h' => Math.Floor(serial * 24).ToString("0", culture),
                    'm' => Math.Floor(serial * 1440).ToString("0", culture),
                    _ => Math.Round(serial * 86400, MidpointRounding.AwayFromZero).ToString("0", culture),
                };
            }

            var length = part.Length;
            switch (part[0])
            {
                case 'y':
                    return length <= 2 ? date.ToString("yy", culture) : date.Year.ToString("0000", culture);
                case 'm' when minute && length <= 2:
                    return date.Minute.ToString(length == 1 ? "0" : "00", culture);
                case 'm':
                    return length switch
                    {
                        1 => date.Month.ToString(culture),
                        2 => date.Month.ToString("00", culture),
                        3 => date.ToString("MMM", culture),
                        4 => date.ToString("MMMM", culture),
                        _ => date.ToString("MMMM", culture).Substring(0, 1),
                    };
                case 'd':
                    return length switch
                    {
                        1 => date.Day.ToString(culture),
                        2 => date.Day.ToString("00", culture),
                        3 => date.ToString("ddd", culture),
                        _ => date.ToString("dddd", culture),
                    };
                case 'a':
                    return length == 3 ? s_koreanDays[(int)date.DayOfWeek] : s_koreanDays[(int)date.DayOfWeek] + "요일";
                case 'h':
                    var hour = twelveHour ? (date.Hour % 12 == 0 ? 12 : date.Hour % 12) : date.Hour;
                    return hour.ToString(length == 1 ? "0" : "00", culture);
                case 's':
                    return date.Second.ToString(length == 1 ? "0" : "00", culture);
                default:
                    return part;
            }
        }

        private static bool IsMinute(List<(char Kind, string Text)> tokens, int index)
        {
            if (tokens[index].Text[0] != 'm')
                return false;

            for (var i = index - 1; i >= 0; i--)
            {
                if (tokens[i].Kind != NumberFormatSection.DatePart)
                    continue;

                if (tokens[i].Text[0] == 'h' || tokens[i].Text.StartsWith("[h", StringComparison.Ordinal))
                    return true;

                break;
            }

            for (var i = index + 1; i < tokens.Count; i++)
            {
                if (tokens[i].Kind != NumberFormatSection.DatePart)
                    continue;

                return tokens[i].Text[0] == 's' || tokens[i].Text.StartsWith("[s", StringComparison.Ordinal);
            }

            return false;
        }
    }
}
