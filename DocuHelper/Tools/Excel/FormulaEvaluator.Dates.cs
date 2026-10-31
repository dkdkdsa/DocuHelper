using System;
using System.Collections.Generic;

namespace DocuHelper.Tools.Excel
{
    internal sealed partial class FormulaEvaluator
    {
        private const double MaxSerial = 2958465;
        private const int MaxWorkdays = 100000;

        private ExcelValue CallDates(string name, List<FormulaNode> args)
        {
            switch (name)
            {
                case "TODAY":
                    return ExcelValue.FromNumber(Math.Floor(ExcelNumberFormat.ToSerial(DateTime.Today)));
                case "NOW":
                    return ExcelValue.FromNumber(ExcelNumberFormat.ToSerial(DateTime.Now));
                case "DATE":
                    return DateOf(args);
                case "YEAR":
                    return DatePart(args[0], d => d.Year, 1900);
                case "MONTH":
                    return DatePart(args[0], d => d.Month, 1);
                case "DAY":
                    return DatePart(args[0], d => d.Day, 0);
                case "WEEKDAY":
                    return Weekday(args);
                case "EDATE":
                    return MonthShift(args, false);
                case "EOMONTH":
                    return MonthShift(args, true);
                case "DATEDIF":
                    return DateDif(args);
                case "NETWORKDAYS":
                    return NetworkDays(args);
                case "WORKDAY":
                    return Workday(args);
                default:
                    return null;
            }
        }

        private ExcelValue SerialArgument(FormulaNode node, out double serial)
        {
            if (!TryNumber(Scalar(node), out serial, out var error))
                return error;

            if (serial < 0 || serial > MaxSerial)
                return ExcelValue.FromError(ExcelValue.BadNumber);

            serial = Math.Floor(serial);
            return null;
        }

        private static DateTime DateAt(double serial) => ExcelNumberFormat.ToDateTime(serial);

        private static ExcelValue SerialOf(DateTime date)
        {
            var serial = Math.Floor(ExcelNumberFormat.ToSerial(date));
            return serial < 0 || serial > MaxSerial ? ExcelValue.FromError(ExcelValue.BadNumber) : ExcelValue.FromNumber(serial);
        }

        private ExcelValue DateOf(List<FormulaNode> args)
        {
            if (!TryInteger(args, 0, 0, out var year, out var error) || !TryInteger(args, 1, 1, out var month, out error) || !TryInteger(args, 2, 1, out var day, out error))
                return error;

            if (year < 0 || year > 9999)
                return ExcelValue.FromError(ExcelValue.BadNumber);

            if (year < 1900)
                year += 1900;

            try
            {
                return SerialOf(new DateTime(year, 1, 1).AddMonths(month - 1).AddDays(day - 1));
            }
            catch (ArgumentOutOfRangeException)
            {
                return ExcelValue.FromError(ExcelValue.BadNumber);
            }
        }

        private ExcelValue DatePart(FormulaNode node, Func<DateTime, int> part, int atZero)
        {
            var problem = SerialArgument(node, out var serial);
            if (problem != null)
                return problem;

            return ExcelValue.FromNumber(serial < 1 ? atZero : part(DateAt(serial)));
        }

        private ExcelValue Weekday(List<FormulaNode> args)
        {
            var problem = SerialArgument(args[0], out var serial);
            if (problem != null)
                return problem;

            if (!TryInteger(args, 1, 1, out var type, out var error))
                return error;

            var sunday = (int)DateAt(serial).DayOfWeek;
            int? result = type switch
            {
                1 or 17 => sunday + 1,
                2 or 11 => ((sunday + 6) % 7) + 1,
                3 => (sunday + 6) % 7,
                12 => ((sunday + 5) % 7) + 1,
                13 => ((sunday + 4) % 7) + 1,
                14 => ((sunday + 3) % 7) + 1,
                15 => ((sunday + 2) % 7) + 1,
                16 => ((sunday + 1) % 7) + 1,
                _ => null,
            };

            return result.HasValue ? ExcelValue.FromNumber(result.Value) : ExcelValue.FromError(ExcelValue.BadNumber);
        }

        private ExcelValue MonthShift(List<FormulaNode> args, bool endOfMonth)
        {
            var problem = SerialArgument(args[0], out var serial);
            if (problem != null)
                return problem;

            if (!TryInteger(args, 1, 0, out var months, out var error))
                return error;

            try
            {
                var date = DateAt(serial).AddMonths(months);
                return SerialOf(endOfMonth ? new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)) : date);
            }
            catch (ArgumentOutOfRangeException)
            {
                return ExcelValue.FromError(ExcelValue.BadNumber);
            }
        }

        private ExcelValue DateDif(List<FormulaNode> args)
        {
            var problem = SerialArgument(args[0], out var startSerial);
            if (problem != null)
                return problem;

            problem = SerialArgument(args[1], out var endSerial);
            if (problem != null)
                return problem;

            problem = TextArgument(args[2], out var unit);
            if (problem != null)
                return problem;

            if (startSerial > endSerial)
                return ExcelValue.FromError(ExcelValue.BadNumber);

            var start = DateAt(startSerial);
            var end = DateAt(endSerial);
            switch (unit.Trim().ToUpperInvariant())
            {
                case "D":
                    return ExcelValue.FromNumber(endSerial - startSerial);
                case "M":
                    var months = ((end.Year - start.Year) * 12) + end.Month - start.Month;
                    return ExcelValue.FromNumber(end.Day < start.Day ? months - 1 : months);
                case "Y":
                    var years = end.Year - start.Year;
                    return ExcelValue.FromNumber(end.Month < start.Month || (end.Month == start.Month && end.Day < start.Day) ? years - 1 : years);
                case "YM":
                    var monthPart = ((end.Month - start.Month) + 12) % 12;
                    return ExcelValue.FromNumber(end.Day < start.Day ? (monthPart + 11) % 12 : monthPart);
                case "MD":
                    if (end.Day >= start.Day)
                        return ExcelValue.FromNumber(end.Day - start.Day);

                    var previous = end.AddMonths(-1);
                    var anchor = new DateTime(previous.Year, previous.Month, Math.Min(start.Day, DateTime.DaysInMonth(previous.Year, previous.Month)));
                    return ExcelValue.FromNumber((end - anchor).Days);
                case "YD":
                    var sameYear = new DateTime(end.Year, start.Month, Math.Min(start.Day, DateTime.DaysInMonth(end.Year, start.Month)));
                    if (sameYear > end)
                        sameYear = new DateTime(end.Year - 1, start.Month, Math.Min(start.Day, DateTime.DaysInMonth(end.Year - 1, start.Month)));

                    return ExcelValue.FromNumber((end - sameYear).Days);
                default:
                    return ExcelValue.FromError(ExcelValue.BadNumber);
            }
        }

        private ExcelValue NetworkDays(List<FormulaNode> args)
        {
            var problem = SerialArgument(args[0], out var start);
            if (problem != null)
                return problem;

            problem = SerialArgument(args[1], out var end);
            if (problem != null)
                return problem;

            problem = Holidays(args, 2, out var holidays);
            if (problem != null)
                return problem;

            var sign = start <= end ? 1 : -1;
            var from = Math.Min(start, end);
            var to = Math.Max(start, end);
            var count = 0;
            for (var day = from; day <= to; day++)
            {
                if (IsWorkday(day, holidays))
                    count++;
            }

            return ExcelValue.FromNumber(sign * count);
        }

        private ExcelValue Workday(List<FormulaNode> args)
        {
            var problem = SerialArgument(args[0], out var day);
            if (problem != null)
                return problem;

            if (!TryInteger(args, 1, 0, out var days, out var error))
                return error;

            if (Math.Abs((long)days) > MaxWorkdays)
                return ExcelValue.FromError(ExcelValue.BadNumber);

            problem = Holidays(args, 2, out var holidays);
            if (problem != null)
                return problem;

            var step = Math.Sign(days);
            for (var remaining = Math.Abs(days); remaining > 0;)
            {
                day += step;
                if (day < 1 || day > MaxSerial)
                    return ExcelValue.FromError(ExcelValue.BadNumber);

                if (IsWorkday(day, holidays))
                    remaining--;
            }

            return ExcelValue.FromNumber(day);
        }

        private ExcelValue Holidays(List<FormulaNode> args, int index, out HashSet<double> holidays)
        {
            holidays = new HashSet<double>();
            if (index >= args.Count || args[index].Kind == FormulaNodeKind.Missing)
                return null;

            var operand = Eval(args[index]);
            var values = operand.IsRange ? Values(operand) : new[] { operand.Value };
            foreach (var value in values)
            {
                if (value.IsBlank)
                    continue;

                if (!TryNumber(value, out var serial, out var error))
                    return error;

                holidays.Add(Math.Floor(serial));
            }

            return null;
        }

        private static bool IsWorkday(double serial, HashSet<double> holidays)
        {
            var day = DateAt(serial).DayOfWeek;
            return day != DayOfWeek.Saturday && day != DayOfWeek.Sunday && !holidays.Contains(serial);
        }
    }
}
