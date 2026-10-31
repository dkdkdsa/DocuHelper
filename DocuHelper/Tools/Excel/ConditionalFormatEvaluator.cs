using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed class ConditionalFormatEvaluator
    {
        public const int MaxCells = 250000;

        private const int FillTaken = 1;
        private const int FontTaken = 2;
        private const int BoldTaken = 4;
        private const int ItalicTaken = 8;
        private const int UnderlineTaken = 16;
        private const int StrikeTaken = 32;
        private const int BorderTaken = 64;
        private const int BarTaken = 128;
        private const int IconTaken = 256;

        private static readonly System.Text.RegularExpressions.Regex s_position = new(
            @"\b(ROW|COLUMN)\(\s*(?:\$?([A-Za-z]{1,3})\$?([0-9]{1,7}))?\s*\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        private readonly ExcelBook _book;
        private readonly ExcelSheet _sheet;
        private readonly ExcelDxfs _dxfs;
        private readonly ExcelColors _colors;
        private readonly DateTime _today;
        private readonly WorkbookCalculator _calculator;
        private readonly Dictionary<long, ExcelValue> _values = new();
        private readonly Dictionary<string, FormulaNode> _parsed = new(StringComparer.Ordinal);
        private List<RuleState> _rules;

        public ConditionalFormatEvaluator(ExcelBook book, ExcelSheet sheet, DateTime? today = null)
        {
            _book = book;
            _sheet = sheet;
            _dxfs = new ExcelDxfs(book.WorkbookPart);
            _colors = new ExcelColors(book.WorkbookPart);
            _today = (today ?? DateTime.Today).Date;
            _calculator = new WorkbookCalculator(book, null, false);
        }

        public bool HasRules => Rules().Count > 0;

        public IReadOnlyDictionary<CellAddress, CellVisual> Evaluate(CellRange area = null)
        {
            var result = new Dictionary<CellAddress, CellVisual>();
            var rules = Rules();
            if (rules.Count == 0)
                return result;

            area ??= _sheet.UsedRange;
            if (area == null)
                return result;

            var slots = new Dictionary<CellAddress, Slot>();
            var budget = MaxCells;
            foreach (var rule in rules)
            {
                foreach (var address in CellsOf(rule, area, ref budget))
                {
                    if (!slots.TryGetValue(address, out var slot))
                    {
                        slot = new Slot();
                        slots[address] = slot;
                    }

                    if (!slot.Stopped)
                        Apply(rule, address.Column, address.Row, slot);
                }
            }

            foreach (var pair in slots)
            {
                if (!pair.Value.Visual.IsEmpty)
                    result[pair.Key] = pair.Value.Visual;
            }

            return result;
        }

        public CellVisual VisualAt(CellAddress address)
        {
            var map = Evaluate(CellRange.Of(address));
            return map.TryGetValue(address, out var visual) ? visual : null;
        }

        public int CountMatches(ConditionalFormattingRule rule, out bool truncated)
        {
            truncated = false;
            var state = Rules().FirstOrDefault(r => ReferenceEquals(r.Rule, rule));
            if (state == null)
                return 0;

            var budget = MaxCells;
            var count = 0;
            foreach (var address in CellsOf(state, null, ref budget))
            {
                var slot = new Slot();
                if (Apply(state, address.Column, address.Row, slot))
                    count++;
            }

            truncated = budget <= 0;
            return count;
        }

        private static IEnumerable<CellAddress> CellsOf(RuleState rule, CellRange area, ref int budget)
        {
            var list = new List<CellAddress>();
            foreach (var rect in rule.Rects)
            {
                var part = area == null ? rect : rect.Intersect(area);
                if (part == null)
                    continue;

                for (var row = part.Top; row <= part.Bottom; row++)
                {
                    for (var column = part.Left; column <= part.Right; column++)
                    {
                        if (budget <= 0)
                            return list;

                        budget--;
                        list.Add(new CellAddress(column, row));
                    }
                }
            }

            return list;
        }

        private List<RuleState> Rules()
        {
            if (_rules != null)
                return _rules;

            var rules = new List<RuleState>();
            var order = 0;
            foreach (var block in _sheet.Worksheet.Elements<ConditionalFormatting>())
            {
                if (!SqrefMath.TryParse(block.SequenceOfReferences?.InnerText, out var rects))
                    continue;

                var bounds = SqrefMath.Bounds(rects);
                foreach (var rule in block.Elements<ConditionalFormattingRule>())
                {
                    if (rule.Type?.Value == null)
                        continue;

                    var state = new RuleState(rule, rects.ToArray(), new CellAddress(bounds.Left, bounds.Top), order++);
                    state.Kind = KindOf(rule);
                    if (state.Kind == Kinds.None)
                        continue;

                    if (rule.FormatId?.Value is uint id)
                        state.Look = _dxfs.Describe(id);

                    rules.Add(state);
                }
            }

            rules.Sort((x, y) => x.Priority != y.Priority ? x.Priority.CompareTo(y.Priority) : x.Order.CompareTo(y.Order));
            _rules = rules;
            return rules;
        }

        private static Kinds KindOf(ConditionalFormattingRule rule)
        {
            var type = rule.Type.Value;
            if (type == ConditionalFormatValues.DataBar)
                return rule.GetFirstChild<DataBar>() == null ? Kinds.None : Kinds.DataBar;

            if (type == ConditionalFormatValues.ColorScale)
                return rule.GetFirstChild<ColorScale>() == null ? Kinds.None : Kinds.ColorScale;

            if (type == ConditionalFormatValues.IconSet)
                return rule.GetFirstChild<IconSet>() == null ? Kinds.None : Kinds.IconSet;

            return Kinds.Highlight;
        }

        private bool Apply(RuleState rule, int column, int row, Slot slot)
        {
            var value = ValueAt(column, row);
            switch (rule.Kind)
            {
                case Kinds.DataBar:
                    return ApplyBar(rule, value, slot);
                case Kinds.ColorScale:
                    return ApplyScale(rule, value, slot);
                case Kinds.IconSet:
                    return ApplyIcon(rule, value, slot);
            }

            if (!Matches(rule, column, row, value))
                return false;

            if (rule.Look != null)
                Merge(slot, rule.Look);

            if (rule.Rule.StopIfTrue?.Value == true)
                slot.Stopped = true;

            return true;
        }

        private static void Merge(Slot slot, ExcelDxfLook look)
        {
            var visual = slot.Visual;
            if (look.Fill != null && (slot.Taken & FillTaken) == 0)
            {
                visual.Fill = look.Fill;
                slot.Taken |= FillTaken;
            }

            if (look.FontColor != null && (slot.Taken & FontTaken) == 0)
            {
                visual.FontColor = look.FontColor;
                slot.Taken |= FontTaken;
            }

            if (look.BorderColor != null && (slot.Taken & BorderTaken) == 0)
            {
                visual.Border = look.BorderColor;
                slot.Taken |= BorderTaken;
            }

            if (look.Bold != null && (slot.Taken & BoldTaken) == 0)
            {
                visual.Bold = look.Bold.Value;
                slot.Taken |= BoldTaken;
            }

            if (look.Italic != null && (slot.Taken & ItalicTaken) == 0)
            {
                visual.Italic = look.Italic.Value;
                slot.Taken |= ItalicTaken;
            }

            if (look.Underline != null && (slot.Taken & UnderlineTaken) == 0)
            {
                visual.Underline = look.Underline.Value;
                slot.Taken |= UnderlineTaken;
            }

            if (look.Strike != null && (slot.Taken & StrikeTaken) == 0)
            {
                visual.Strike = look.Strike.Value;
                slot.Taken |= StrikeTaken;
            }
        }

        private bool ApplyBar(RuleState rule, ExcelValue value, Slot slot)
        {
            if (!value.IsNumber)
                return false;

            var bar = rule.Rule.GetFirstChild<DataBar>();
            var stats = StatsOf(rule);
            if (stats.Numbers.Length == 0 || (slot.Taken & BarTaken) != 0)
                return false;

            var cfvos = bar.Elements<ConditionalFormatValueObject>().ToList();
            var low = cfvos.Count > 0 ? Threshold(rule, cfvos[0], stats) : null;
            var high = cfvos.Count > 1 ? Threshold(rule, cfvos[1], stats) : null;
            low ??= stats.Min;
            high ??= stats.Max;
            var minLength = Math.Clamp(bar.MinLength?.Value ?? 10u, 0u, 100u);
            var maxLength = Math.Clamp(bar.MaxLength?.Value ?? 90u, minLength, 100u);
            var ratio = high.Value > low.Value ? Math.Clamp((value.Number - low.Value) / (high.Value - low.Value), 0, 1) : 1;
            slot.Visual.BarFraction = (minLength + ((maxLength - minLength) * ratio)) / 100.0;
            slot.Visual.BarColor = _colors.Resolve(bar.Elements<Color>().FirstOrDefault()) ?? "638EC6";
            if (bar.ShowValue?.Value == false)
                slot.Visual.HideValue = true;

            slot.Taken |= BarTaken;
            return true;
        }

        private bool ApplyScale(RuleState rule, ExcelValue value, Slot slot)
        {
            if (!value.IsNumber)
                return false;

            var scale = rule.Rule.GetFirstChild<ColorScale>();
            var stats = StatsOf(rule);
            var cfvos = scale.Elements<ConditionalFormatValueObject>().ToList();
            var colors = scale.Elements<Color>().Select(c => _colors.Resolve(c)).ToList();
            if (stats.Numbers.Length == 0 || cfvos.Count < 2 || colors.Count < cfvos.Count || colors.Any(c => c == null))
                return false;

            var points = new List<double>();
            for (var i = 0; i < cfvos.Count; i++)
            {
                var point = Threshold(rule, cfvos[i], stats);
                if (point == null)
                    return false;

                points.Add(point.Value);
            }

            var fill = Interpolate(value.Number, points, colors);
            if ((slot.Taken & FillTaken) != 0)
                return true;

            slot.Visual.ScaleFill = fill;
            slot.Taken |= FillTaken;
            return true;
        }

        private bool ApplyIcon(RuleState rule, ExcelValue value, Slot slot)
        {
            if (!value.IsNumber)
                return false;

            var set = rule.Rule.GetFirstChild<IconSet>();
            var stats = StatsOf(rule);
            var name = set.GetAttributes().FirstOrDefault(a => a.LocalName == "iconSet" && string.IsNullOrEmpty(a.NamespaceUri)).Value ?? "3TrafficLights1";
            var cfvos = set.Elements<ConditionalFormatValueObject>().ToList();
            if (stats.Numbers.Length == 0 || cfvos.Count < 2 || (slot.Taken & IconTaken) != 0)
                return false;

            var number = ExcelValue.Round15(value.Number);
            var index = 0;
            for (var i = cfvos.Count - 1; i >= 1; i--)
            {
                var point = Threshold(rule, cfvos[i], stats);
                if (point == null)
                    continue;

                var limit = ExcelValue.Round15(point.Value);
                var reached = cfvos[i].GreaterThanOrEqual?.Value == false ? number > limit : number >= limit;
                if (reached)
                {
                    index = i;
                    break;
                }
            }

            if (set.Reverse?.Value == true)
                index = cfvos.Count - 1 - index;

            slot.Visual.IconSet = name;
            slot.Visual.IconIndex = index;
            slot.Visual.IconCount = cfvos.Count;
            if (set.ShowValue?.Value == false)
                slot.Visual.HideValue = true;

            slot.Taken |= IconTaken;
            return true;
        }

        private static string Interpolate(double value, List<double> points, List<string> colors)
        {
            if (value <= points[0])
                return colors[0];

            var last = points.Count - 1;
            if (value >= points[last])
                return colors[last];

            for (var i = 1; i <= last; i++)
            {
                if (value > points[i])
                    continue;

                var span = points[i] - points[i - 1];
                var ratio = span > 0 ? (value - points[i - 1]) / span : 1;
                return Blend(colors[i - 1], colors[i], ratio);
            }

            return colors[last];
        }

        private static string Blend(string from, string to, double ratio)
        {
            var text = new System.Text.StringBuilder();
            for (var channel = 0; channel < 3; channel++)
            {
                var a = int.Parse(from.Substring(channel * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var b = int.Parse(to.Substring(channel * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var mixed = (int)Math.Round(a + ((b - a) * ratio), MidpointRounding.AwayFromZero);
                text.Append(Math.Clamp(mixed, 0, 255).ToString("X2", CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }

        private double? Threshold(RuleState rule, ConditionalFormatValueObject cfvo, Stats stats)
        {
            var type = cfvo.Type?.InnerText;
            var text = cfvo.Val?.Value;
            switch (type)
            {
                case "min":
                case "autoMin":
                    return stats.Min;
                case "max":
                case "autoMax":
                    return stats.Max;
                case "percent":
                    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) ? stats.Min + ((stats.Max - stats.Min) * percent / 100) : null;
                case "percentile":
                    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var rank) ? Percentile(stats.Numbers, rank) : null;
                case "num":
                case "formula":
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                        return number;

                    var evaluated = string.IsNullOrWhiteSpace(text) ? null : Evaluate(rule, text, rule.Base.Column, rule.Base.Row);
                    return evaluated != null && evaluated.IsNumber ? evaluated.Number : null;
                default:
                    return null;
            }
        }

        private static double Percentile(double[] sorted, double percent)
        {
            var position = (sorted.Length - 1) * Math.Clamp(percent, 0, 100) / 100;
            var low = (int)Math.Floor(position);
            var high = (int)Math.Ceiling(position);
            return sorted[low] + ((sorted[high] - sorted[low]) * (position - low));
        }

        private bool Matches(RuleState rule, int column, int row, ExcelValue value)
        {
            var type = rule.Rule.Type.Value;
            if (type == ConditionalFormatValues.CellIs)
                return CellIs(rule, column, row, value);

            if (type == ConditionalFormatValues.Expression)
                return Truthy(FirstFormula(rule, column, row));

            if (type == ConditionalFormatValues.ContainsBlanks)
                return IsBlank(value);

            if (type == ConditionalFormatValues.NotContainsBlanks)
                return !IsBlank(value);

            if (type == ConditionalFormatValues.ContainsErrors)
                return value.IsError;

            if (type == ConditionalFormatValues.NotContainsErrors)
                return !value.IsError;

            if (type == ConditionalFormatValues.ContainsText || type == ConditionalFormatValues.NotContainsText
                || type == ConditionalFormatValues.BeginsWith || type == ConditionalFormatValues.EndsWith)
                return TextMatch(rule, type, column, row, value);

            if (type == ConditionalFormatValues.DuplicateValues || type == ConditionalFormatValues.UniqueValues)
                return Repeats(rule, type, value);

            if (type == ConditionalFormatValues.Top10)
                return TopBottom(rule, value);

            if (type == ConditionalFormatValues.AboveAverage)
                return Average(rule, value);

            if (type == ConditionalFormatValues.TimePeriod)
                return value.IsNumber && InPeriod(rule.Rule.TimePeriod?.Value, value.Number);

            return false;
        }

        private bool CellIs(RuleState rule, int column, int row, ExcelValue value)
        {
            var formulas = rule.Rule.Elements<Formula>().ToList();
            if (formulas.Count == 0 || value.IsError)
                return false;

            var first = Evaluate(rule, formulas[0].Text, column, row);
            if (first == null || first.IsError)
                return false;

            var op = rule.Rule.Operator?.Value ?? ConditionalFormattingOperatorValues.Equal;
            if (op == ConditionalFormattingOperatorValues.Between || op == ConditionalFormattingOperatorValues.NotBetween)
            {
                if (formulas.Count < 2)
                    return false;

                var second = Evaluate(rule, formulas[1].Text, column, row);
                var low = Compare(value, first);
                var high = second == null ? null : Compare(value, second);
                if (low == null || high == null)
                    return false;

                var inside = low.Value >= 0 && high.Value <= 0;
                return op == ConditionalFormattingOperatorValues.Between ? inside : !inside;
            }

            var order = Compare(value, first);
            if (order == null)
                return false;

            if (op == ConditionalFormattingOperatorValues.GreaterThan)
                return order.Value > 0;

            if (op == ConditionalFormattingOperatorValues.GreaterThanOrEqual)
                return order.Value >= 0;

            if (op == ConditionalFormattingOperatorValues.LessThan)
                return order.Value < 0;

            if (op == ConditionalFormattingOperatorValues.LessThanOrEqual)
                return order.Value <= 0;

            if (op == ConditionalFormattingOperatorValues.NotEqual)
                return order.Value != 0;

            return order.Value == 0;
        }

        private static int? Compare(ExcelValue left, ExcelValue right)
        {
            if (left.IsError || right.IsError)
                return null;

            if (left.IsBlank)
                left = right.IsText ? ExcelValue.FromText(string.Empty) : right.IsBoolean ? ExcelValue.False : ExcelValue.Zero;

            if (right.IsBlank)
                right = left.IsText ? ExcelValue.FromText(string.Empty) : left.IsBoolean ? ExcelValue.False : ExcelValue.Zero;

            var a = Rank(left);
            var b = Rank(right);
            if (a != b)
                return a.CompareTo(b);

            if (left.IsText)
                return Math.Sign(string.Compare(left.Text, right.Text, StringComparison.OrdinalIgnoreCase));

            return ExcelValue.Round15(left.Number).CompareTo(ExcelValue.Round15(right.Number));
        }

        private static int Rank(ExcelValue value) => value.IsText ? 1 : value.IsBoolean ? 2 : 0;

        private static bool IsBlank(ExcelValue value) => value.IsBlank || (value.IsText && value.Text.Trim(' ').Length == 0);

        private ExcelValue FirstFormula(RuleState rule, int column, int row)
        {
            var formula = rule.Rule.GetFirstChild<Formula>();
            return formula == null ? null : Evaluate(rule, formula.Text, column, row);
        }

        private static bool Truthy(ExcelValue value) =>
            value != null && ((value.IsBoolean && value.Boolean) || (value.IsNumber && value.Number != 0));

        private bool TextMatch(RuleState rule, ConditionalFormatValues type, int column, int row, ExcelValue value)
        {
            var evaluated = FirstFormula(rule, column, row);
            if (evaluated != null && !evaluated.IsError)
                return Truthy(evaluated);

            var needle = rule.Rule.Text?.Value ?? string.Empty;
            var text = value.IsNumber ? ExcelValue.Invariant(value.Number) : value.IsBoolean ? (value.Boolean ? "TRUE" : "FALSE") : value.Text ?? string.Empty;
            if (type == ConditionalFormatValues.BeginsWith)
                return text.StartsWith(needle, StringComparison.OrdinalIgnoreCase);

            if (type == ConditionalFormatValues.EndsWith)
                return text.EndsWith(needle, StringComparison.OrdinalIgnoreCase);

            var found = text.Contains(needle, StringComparison.OrdinalIgnoreCase);
            return type == ConditionalFormatValues.ContainsText ? found : !found;
        }

        private bool Repeats(RuleState rule, ConditionalFormatValues type, ExcelValue value)
        {
            if (IsBlank(value))
                return false;

            var stats = StatsOf(rule);
            stats.Counts.TryGetValue(KeyOf(value), out var count);
            return type == ConditionalFormatValues.DuplicateValues ? count > 1 : count == 1;
        }

        private static string KeyOf(ExcelValue value) => value.Kind switch
        {
            ExcelValueKind.Number => "n" + ExcelValue.Invariant(value.Number),
            ExcelValueKind.Boolean => "b" + (value.Boolean ? "1" : "0"),
            ExcelValueKind.Error => "e" + value.Text,
            _ => "t" + (value.Text ?? string.Empty).ToUpperInvariant(),
        };

        private bool TopBottom(RuleState rule, ExcelValue value)
        {
            if (!value.IsNumber)
                return false;

            var stats = StatsOf(rule);
            var count = stats.Numbers.Length;
            if (count == 0)
                return false;

            var rank = (int)Math.Min(rule.Rule.Rank?.Value ?? 10u, int.MaxValue);
            var take = rule.Rule.Percent?.Value == true ? Math.Max(1, (int)Math.Floor(count * rank / 100.0)) : rank;
            take = Math.Min(Math.Max(take, 1), count);
            var number = ExcelValue.Round15(value.Number);
            if (rule.Rule.Bottom?.Value == true)
                return number <= ExcelValue.Round15(stats.Numbers[take - 1]);

            return number >= ExcelValue.Round15(stats.Numbers[count - take]);
        }

        private bool Average(RuleState rule, ExcelValue value)
        {
            if (!value.IsNumber)
                return false;

            var stats = StatsOf(rule);
            if (stats.Numbers.Length == 0)
                return false;

            var deviations = (int)(rule.Rule.StdDev?.Value ?? 0);
            if (deviations > 0 && stats.Numbers.Length < 2)
                return false;

            var above = rule.Rule.AboveAverage?.Value != false;
            var equal = rule.Rule.EqualAverage?.Value == true;
            var number = ExcelValue.Round15(value.Number);
            var limit = ExcelValue.Round15(above ? stats.Average + (deviations * stats.Deviation) : stats.Average - (deviations * stats.Deviation));
            if (above)
                return equal ? number >= limit : number > limit;

            return equal ? number <= limit : number < limit;
        }

        private bool InPeriod(TimePeriodValues? period, double serial)
        {
            if (period == null)
                return false;

            var day = Math.Floor(serial);
            var today = Math.Floor(ExcelNumberFormat.ToSerial(_today));
            var weekStart = today - (int)_today.DayOfWeek;
            var p = period.Value;
            if (p == TimePeriodValues.Today)
                return day == today;

            if (p == TimePeriodValues.Yesterday)
                return day == today - 1;

            if (p == TimePeriodValues.Tomorrow)
                return day == today + 1;

            if (p == TimePeriodValues.Last7Days)
                return day >= today - 6 && day <= today;

            if (p == TimePeriodValues.ThisWeek)
                return day >= weekStart && day <= weekStart + 6;

            if (p == TimePeriodValues.LastWeek)
                return day >= weekStart - 7 && day <= weekStart - 1;

            if (p == TimePeriodValues.NextWeek)
                return day >= weekStart + 7 && day <= weekStart + 13;

            if (serial < 1)
                return false;

            var date = ExcelNumberFormat.ToDateTime(day);
            var offset = p == TimePeriodValues.LastMonth ? -1 : p == TimePeriodValues.NextMonth ? 1 : 0;
            var target = new DateTime(_today.Year, _today.Month, 1).AddMonths(offset);
            return date.Year == target.Year && date.Month == target.Month;
        }

        private ExcelValue Evaluate(RuleState rule, string formula, int column, int row)
        {
            if (string.IsNullOrWhiteSpace(formula))
                return null;

            try
            {
                var text = formula;
                var rowDelta = row - rule.Base.Row;
                var columnDelta = column - rule.Base.Column;
                if ((rowDelta != 0 || columnDelta != 0) && !FormulaText.TryShift(formula, rowDelta, columnDelta, out text))
                    return ExcelValue.FromError(ExcelValue.BadReference);

                text = ExpandPosition(text, column, row);
                if (!_parsed.TryGetValue(text, out var node))
                {
                    FormulaParser.TryParse(text, out node);
                    _parsed[text] = node;
                }

                return node == null ? null : new FormulaEvaluator(_calculator, _sheet, column, row).Evaluate(node);
            }
            catch (FormulaException)
            {
                return null;
            }
            catch (UnknownValueException)
            {
                return null;
            }
            catch (CalculationBudgetException)
            {
                return null;
            }
        }

        private static string ExpandPosition(string text, int column, int row)
        {
            if (text.IndexOf("ROW", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("COLUMN", StringComparison.OrdinalIgnoreCase) < 0)
                return text;

            var parts = text.Split('"');
            for (var i = 0; i < parts.Length; i += 2)
            {
                parts[i] = s_position.Replace(parts[i], match =>
                {
                    var isRow = match.Groups[1].Value.Equals("ROW", StringComparison.OrdinalIgnoreCase);
                    if (!match.Groups[2].Success)
                        return (isRow ? row + 1 : column + 1).ToString(CultureInfo.InvariantCulture);

                    return CellAddress.TryParse(match.Groups[2].Value + match.Groups[3].Value, out var address)
                        ? (isRow ? address.Row + 1 : address.Column + 1).ToString(CultureInfo.InvariantCulture)
                        : match.Value;
                });
            }

            return string.Join("\"", parts);
        }

        private ExcelValue ValueAt(int column, int row)
        {
            var key = ((long)row * CellAddress.MaxColumns) + column;
            if (_values.TryGetValue(key, out var cached))
                return cached;

            var cell = _sheet.FindCell(column, row);
            ExcelValue value;
            if (cell == null)
            {
                value = ExcelValue.Blank;
            }
            else if (cell.CellFormula != null)
            {
                try
                {
                    value = _calculator.GetValue(_sheet, column, row);
                }
                catch (UnknownValueException)
                {
                    value = _book.ReadValue(cell);
                }
                catch (CalculationBudgetException)
                {
                    value = _book.ReadValue(cell);
                }
            }
            else
            {
                value = _book.ReadValue(cell);
            }

            _values[key] = value;
            return value;
        }

        private Stats StatsOf(RuleState rule)
        {
            if (rule.Stats != null)
                return rule.Stats;

            var numbers = new List<double>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in _sheet.Entries)
            {
                if (!rule.Contains(entry.Column, entry.Row))
                    continue;

                var value = ValueAt(entry.Column, entry.Row);
                if (value.IsNumber)
                    numbers.Add(value.Number);

                if (!IsBlank(value))
                {
                    var key = KeyOf(value);
                    counts[key] = counts.TryGetValue(key, out var seen) ? seen + 1 : 1;
                }
            }

            numbers.Sort();
            var stats = new Stats { Numbers = numbers.ToArray(), Counts = counts };
            if (numbers.Count > 0)
            {
                stats.Min = numbers[0];
                stats.Max = numbers[^1];
                stats.Average = numbers.Average();
                stats.Deviation = numbers.Count > 1 ? Math.Sqrt(numbers.Sum(n => (n - stats.Average) * (n - stats.Average)) / (numbers.Count - 1)) : 0;
            }

            rule.Stats = stats;
            return stats;
        }

        private enum Kinds
        {
            None,
            Highlight,
            DataBar,
            ColorScale,
            IconSet,
        }

        private sealed class Slot
        {
            public CellVisual Visual { get; } = new();

            public int Taken { get; set; }

            public bool Stopped { get; set; }
        }

        private sealed class Stats
        {
            public double[] Numbers { get; init; }

            public Dictionary<string, int> Counts { get; init; }

            public double Min { get; set; }

            public double Max { get; set; }

            public double Average { get; set; }

            public double Deviation { get; set; }
        }

        private sealed class RuleState
        {
            public RuleState(ConditionalFormattingRule rule, CellRange[] rects, CellAddress origin, int order)
            {
                Rule = rule;
                Rects = rects;
                Base = origin;
                Order = order;
                Priority = rule.Priority?.Value ?? int.MaxValue;
            }

            public ConditionalFormattingRule Rule { get; }

            public CellRange[] Rects { get; }

            public CellAddress Base { get; }

            public int Order { get; }

            public int Priority { get; }

            public Kinds Kind { get; set; }

            public ExcelDxfLook Look { get; set; }

            public Stats Stats { get; set; }

            public bool Contains(int column, int row)
            {
                foreach (var rect in Rects)
                {
                    if (rect.Contains(column, row))
                        return true;
                }

                return false;
            }
        }
    }
}
