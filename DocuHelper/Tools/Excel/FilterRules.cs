using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal static class FilterRules
    {
        public const string BlankLabel = "(빈 셀)";

        public const int MaxValues = 5000;

        private static readonly string[] s_blankLabels = { BlankLabel, "(비어 있음)", "(Blanks)" };

        private static readonly CompareInfo s_compare = CreateCompare();

        public static bool HasCriteria(FilterColumn column) =>
            column.ChildElements.Any(c => c is Filters or CustomFilters or Top10 or DynamicFilter or ColorFilter or IconFilter);

        public static FilterColumn Build(ToolInput input, uint columnId)
        {
            var hasValues = input.Has("values");
            var hasCondition = input.Has("condition");
            var values = hasValues ? input.GetStringList("values") : new List<string>();
            if (hasCondition && values.Count > 0)
                throw new ToolInputException("values와 condition은 한 번에 하나만 쓸 수 있어요. 고르는 값 목록이면 values, 크기 비교나 글자 조건이면 condition으로 알려 주세요.");

            var column = new FilterColumn { ColumnId = columnId };
            if (values.Count > 0)
            {
                column.Append(BuildValues(values));
                return column;
            }

            if (!hasCondition)
                return null;

            var condition = input.Get("condition");
            if (condition.ValueKind != JsonValueKind.Object)
                throw new ToolInputException("condition은 {\"operator\": \"greater_than\", \"value\": \"100\"}처럼 알려 주세요.");

            column.Append(BuildCondition(condition));
            return column;
        }

        public static string Describe(FilterColumn column)
        {
            var filters = column.GetFirstChild<Filters>();
            if (filters != null)
            {
                var items = filters.Elements<Filter>().Select(f => f.Val?.Value ?? string.Empty).ToList();
                if (filters.Blank?.Value == true)
                    items.Add(BlankLabel);

                if (items.Count == 0 && filters.Blank?.Value != true)
                    return "values (none)";

                var shown = items.Take(6).Select(i => "\"" + i + "\"");
                return "is one of " + string.Join(", ", shown) + (items.Count > 6 ? $" and {items.Count - 6} more" : string.Empty);
            }

            var custom = column.GetFirstChild<CustomFilters>();
            if (custom != null)
            {
                var parts = custom.Elements<CustomFilter>().Select(c => OperatorName(c.Operator?.Value) + " \"" + (c.Val?.Value ?? string.Empty) + "\"");
                return string.Join(custom.And?.Value == true ? " and " : " or ", parts);
            }

            var top = column.GetFirstChild<Top10>();
            if (top != null)
                return $"{((top.Top?.Value ?? true) ? "top" : "bottom")} {(top.Val?.Value ?? 10).ToString("0.##", CultureInfo.InvariantCulture)}{(top.Percent?.Value == true ? " percent" : " items")}";

            var dynamic = column.GetFirstChild<DynamicFilter>();
            return dynamic?.Type?.InnerText ?? "custom";
        }

        public static Func<string, ExcelValue, bool> Compile(FilterColumn column, IReadOnlyList<(string Text, ExcelValue Value)> cells, string label)
        {
            if (column.GetFirstChild<ColorFilter>() != null || column.GetFirstChild<IconFilter>() != null)
                throw Unsupported(label);

            var filters = column.GetFirstChild<Filters>();
            if (filters != null)
            {
                if (filters.Elements<DateGroupItem>().Any())
                    throw Unsupported(label);

                return CompileValues(filters);
            }

            var custom = column.GetFirstChild<CustomFilters>();
            if (custom != null)
                return CompileCustom(custom);

            var top = column.GetFirstChild<Top10>();
            if (top != null)
                return CompileTop(top, cells);

            var dynamic = column.GetFirstChild<DynamicFilter>();
            if (dynamic != null)
                return CompileDynamic(dynamic, cells, label);

            return null;
        }

        private static ToolInputException Unsupported(string label) =>
            new($"{label}에 걸린 필터(날짜 묶음, 색, 아이콘 등)는 이 도구가 다시 계산할 수 없어요. 그 열의 조건을 clear_criteria로 지운 뒤 다시 해 주세요.");

        private static Filters BuildValues(IReadOnlyList<string> values)
        {
            var filters = new Filters();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in values)
            {
                if (value == null)
                    continue;

                if (s_blankLabels.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    filters.Blank = true;
                    continue;
                }

                if (seen.Add(value))
                    filters.Append(new Filter { Val = value });
            }

            if (seen.Count + (filters.Blank?.Value == true ? 1 : 0) > MaxValues)
                throw new ToolInputException($"values는 {MaxValues}개까지만 쓸 수 있어요. 값이 많으면 condition으로 조건을 알려 주세요.");

            return filters;
        }

        private static Func<string, ExcelValue, bool> CompileValues(Filters filters)
        {
            var blank = filters.Blank?.Value == true;
            var texts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var numbers = new HashSet<double>();
            foreach (var filter in filters.Elements<Filter>())
            {
                var text = filter.Val?.Value ?? string.Empty;
                texts.Add(text);
                if (TypedInput.TryParseNumber(text, out var number, out _))
                    numbers.Add(number);
            }

            return (text, value) =>
            {
                if (IsBlank(text, value))
                    return blank || texts.Contains(string.Empty);

                return texts.Contains(text) || (value.IsNumber && (numbers.Contains(value.Number) || texts.Contains(value.ToRawText())));
            };
        }

        private static Func<string, ExcelValue, bool> CompileCustom(CustomFilters custom)
        {
            var tests = custom.Elements<CustomFilter>()
                .Select(c => Test(c.Operator?.Value ?? FilterOperatorValues.Equal, c.Val?.Value ?? string.Empty))
                .ToList();
            if (tests.Count == 0)
                return null;

            if (custom.And?.Value == true)
                return (text, value) => tests.All(t => t(text, value));

            return (text, value) => tests.Any(t => t(text, value));
        }

        private static Func<string, ExcelValue, bool> Test(FilterOperatorValues op, string val)
        {
            if (op == FilterOperatorValues.Equal || op == FilterOperatorValues.NotEqual)
            {
                var equal = EqualTest(val);
                return op == FilterOperatorValues.Equal ? equal : (text, value) => !equal(text, value);
            }

            var numeric = TypedInput.TryParseNumber(val, out var number, out _);
            var literal = Unescape(val);
            return (text, value) =>
            {
                if (IsBlank(text, value))
                    return false;

                int comparison;
                if (numeric)
                {
                    if (!value.IsNumber)
                        return false;

                    comparison = value.Number.CompareTo(number);
                }
                else
                {
                    if (!value.IsText)
                        return false;

                    comparison = s_compare.Compare(text, literal, CompareOptions.IgnoreCase);
                }

                if (op == FilterOperatorValues.GreaterThan)
                    return comparison > 0;

                if (op == FilterOperatorValues.GreaterThanOrEqual)
                    return comparison >= 0;

                if (op == FilterOperatorValues.LessThan)
                    return comparison < 0;

                return comparison <= 0;
            };
        }

        private static Func<string, ExcelValue, bool> EqualTest(string val)
        {
            if (val == " ")
                return IsBlank;

            if (HasWildcard(val))
            {
                var pattern = new Regex(WildcardPattern(val), RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
                return (text, value) => !IsBlank(text, value) && pattern.IsMatch(text);
            }

            var literal = Unescape(val);
            var numeric = TypedInput.TryParseNumber(val, out var number, out _);
            return (text, value) =>
            {
                if (value.IsNumber && numeric)
                    return value.Number == number;

                return !IsBlank(text, value) && string.Equals(text, literal, StringComparison.OrdinalIgnoreCase);
            };
        }

        private static Func<string, ExcelValue, bool> CompileTop(Top10 top, IReadOnlyList<(string Text, ExcelValue Value)> cells)
        {
            var numbers = cells.Where(c => c.Value.IsNumber).Select(c => c.Value.Number).ToList();
            var isTop = top.Top?.Value ?? true;
            var amount = top.Val?.Value ?? 10;
            var count = top.Percent?.Value == true ? (int)Math.Floor(numbers.Count * amount / 100.0) : (int)Math.Floor(amount);
            count = Math.Max(1, count);
            if (numbers.Count == 0)
                return (_, _) => false;

            numbers.Sort();
            var cutoff = isTop ? numbers[Math.Max(0, numbers.Count - count)] : numbers[Math.Min(numbers.Count - 1, count - 1)];
            top.FilterValue = cutoff;
            if (isTop)
                return (_, value) => value.IsNumber && value.Number >= cutoff;

            return (_, value) => value.IsNumber && value.Number <= cutoff;
        }

        private static Func<string, ExcelValue, bool> CompileDynamic(DynamicFilter dynamic, IReadOnlyList<(string Text, ExcelValue Value)> cells, string label)
        {
            var type = dynamic.Type?.Value;
            if (type != DynamicFilterValues.AboveAverage && type != DynamicFilterValues.BelowAverage)
                throw Unsupported(label);

            var numbers = cells.Where(c => c.Value.IsNumber).Select(c => c.Value.Number).ToList();
            if (numbers.Count == 0)
                return (_, _) => false;

            var average = numbers.Average();
            dynamic.Val = average;
            if (type == DynamicFilterValues.AboveAverage)
                return (_, value) => value.IsNumber && value.Number > average;

            return (_, value) => value.IsNumber && value.Number < average;
        }

        private static OpenXmlElement BuildCondition(JsonElement condition)
        {
            var name = Text(condition, "operator")?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(name))
                throw new ToolInputException("condition.operator가 필요해요. equal, not_equal, greater_than, greater_equal, less_than, less_equal, begins_with, ends_with, contains, not_contains, between, top, bottom, above_average, below_average, blank, non_blank 중 하나로 알려 주세요.");

            switch (name)
            {
                case "top":
                case "bottom":
                    return BuildTop(condition, name == "top");
                case "above_average":
                case "below_average":
                    Only(condition, name);
                    return new DynamicFilter { Type = name == "above_average" ? DynamicFilterValues.AboveAverage : DynamicFilterValues.BelowAverage };
                case "blank":
                    Only(condition, name);
                    return new Filters { Blank = true };
                case "non_blank":
                    Only(condition, name);
                    return new CustomFilters(new CustomFilter { Operator = FilterOperatorValues.NotEqual, Val = " " });
                case "between":
                    return BuildBetween(condition);
            }

            var filters = new CustomFilters();
            filters.Append(Comparison(name, Required(condition, "value", name)));
            var second = Text(condition, "operator2")?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(second))
            {
                filters.Append(Comparison(second, Required(condition, "value3", second)));
                if ((Text(condition, "combine") ?? "and").Trim().ToLowerInvariant() == "and")
                    filters.And = true;
            }

            return filters;
        }

        private static Top10 BuildTop(JsonElement condition, bool top)
        {
            var count = 10;
            if (condition.TryGetProperty("count", out var countValue) && countValue.ValueKind != JsonValueKind.Null)
            {
                if (!(countValue.ValueKind == JsonValueKind.Number && countValue.TryGetInt32(out count))
                    && !(countValue.ValueKind == JsonValueKind.String && int.TryParse(countValue.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count)))
                    throw new ToolInputException("condition.count는 정수여야 해요.");
            }

            var percent = condition.TryGetProperty("percent", out var percentValue) && (percentValue.ValueKind == JsonValueKind.True || (percentValue.ValueKind == JsonValueKind.String && bool.TryParse(percentValue.GetString(), out var flag) && flag));
            if (count < 1 || count > 1000 || (percent && count > 100))
                throw new ToolInputException(percent ? "퍼센트는 1부터 100 사이로 알려 주세요." : "개수는 1부터 1000 사이로 알려 주세요.");

            var element = new Top10 { Val = count };
            if (!top)
                element.Top = false;

            if (percent)
                element.Percent = true;

            return element;
        }

        private static CustomFilters BuildBetween(JsonElement condition)
        {
            var low = Required(condition, "value", "between");
            var high = Required(condition, "value2", "between");
            var lowNumeric = TypedInput.TryParseNumber(low, out var lowNumber, out _);
            var highNumeric = TypedInput.TryParseNumber(high, out var highNumber, out _);
            if (lowNumeric != highNumeric)
                throw new ToolInputException("between의 value와 value2는 둘 다 숫자이거나 둘 다 글자여야 해요.");

            if (lowNumeric ? lowNumber > highNumber : s_compare.Compare(low, high, CompareOptions.IgnoreCase) > 0)
                throw new ToolInputException("between의 value는 value2보다 작거나 같아야 해요.");

            if (Text(condition, "operator2") != null)
                throw new ToolInputException("between에는 두 번째 조건(operator2)을 더할 수 없어요.");

            return new CustomFilters(
                new CustomFilter { Operator = FilterOperatorValues.GreaterThanOrEqual, Val = Ordered(low) },
                new CustomFilter { Operator = FilterOperatorValues.LessThanOrEqual, Val = Ordered(high) })
            { And = true };
        }

        private static CustomFilter Comparison(string name, string value)
        {
            switch (name)
            {
                case "equal":
                    return new CustomFilter { Val = Literal(value) };
                case "not_equal":
                    return new CustomFilter { Operator = FilterOperatorValues.NotEqual, Val = Literal(value) };
                case "greater_than":
                    return new CustomFilter { Operator = FilterOperatorValues.GreaterThan, Val = Ordered(value) };
                case "greater_equal":
                    return new CustomFilter { Operator = FilterOperatorValues.GreaterThanOrEqual, Val = Ordered(value) };
                case "less_than":
                    return new CustomFilter { Operator = FilterOperatorValues.LessThan, Val = Ordered(value) };
                case "less_equal":
                    return new CustomFilter { Operator = FilterOperatorValues.LessThanOrEqual, Val = Ordered(value) };
                case "begins_with":
                    return new CustomFilter { Val = Escape(value) + "*" };
                case "ends_with":
                    return new CustomFilter { Val = "*" + Escape(value) };
                case "contains":
                    return new CustomFilter { Val = "*" + Escape(value) + "*" };
                case "not_contains":
                    return new CustomFilter { Operator = FilterOperatorValues.NotEqual, Val = "*" + Escape(value) + "*" };
                default:
                    throw new ToolInputException($"알 수 없는 조건이에요: {name}. equal, not_equal, greater_than, greater_equal, less_than, less_equal, begins_with, ends_with, contains, not_contains, between, top, bottom, above_average, below_average, blank, non_blank 중에서 골라 주세요.");
            }
        }

        private static string Literal(string value) =>
            TypedInput.TryParseNumber(value, out var number, out _) ? ExcelValue.Invariant(number) : Escape(value);

        private static string Ordered(string value) =>
            TypedInput.TryParseNumber(value, out var number, out _) ? ExcelValue.Invariant(number) : value;

        private static void Only(JsonElement condition, string name)
        {
            if (Text(condition, "value") != null || Text(condition, "operator2") != null)
                throw new ToolInputException($"{name} 조건에는 value나 두 번째 조건을 쓸 수 없어요.");
        }

        private static string Required(JsonElement condition, string field, string name)
        {
            var text = Text(condition, field);
            if (string.IsNullOrEmpty(text))
                throw new ToolInputException($"{name} 조건에는 condition.{field} 값이 필요해요.");

            return text;
        }

        private static string Text(JsonElement condition, string field) =>
            condition.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null ? ToolInput.AsText(value) : null;

        private static bool IsBlank(string text, ExcelValue value) => value.IsBlank || text.Length == 0;

        private static string OperatorName(FilterOperatorValues? op)
        {
            if (op == FilterOperatorValues.NotEqual)
                return "not equal to";

            if (op == FilterOperatorValues.GreaterThan)
                return "greater than";

            if (op == FilterOperatorValues.GreaterThanOrEqual)
                return "at least";

            if (op == FilterOperatorValues.LessThan)
                return "less than";

            if (op == FilterOperatorValues.LessThanOrEqual)
                return "at most";

            return "equal to";
        }

        private static string Escape(string value)
        {
            var text = new StringBuilder(value.Length + 4);
            foreach (var c in value)
            {
                if (c is '*' or '?' or '~')
                    text.Append('~');

                text.Append(c);
            }

            return text.ToString();
        }

        private static string Unescape(string value)
        {
            if (value.IndexOf('~') < 0)
                return value;

            var text = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == '~' && i + 1 < value.Length && value[i + 1] is '*' or '?' or '~')
                    i++;

                text.Append(value[i]);
            }

            return text.ToString();
        }

        private static bool HasWildcard(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == '~')
                    i++;
                else if (value[i] is '*' or '?')
                    return true;
            }

            return false;
        }

        private static string WildcardPattern(string value)
        {
            var pattern = new StringBuilder("^");
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == '~' && i + 1 < value.Length && value[i + 1] is '*' or '?' or '~')
                    pattern.Append(Regex.Escape(value[++i].ToString()));
                else if (c == '*')
                    pattern.Append(".*");
                else if (c == '?')
                    pattern.Append('.');
                else
                    pattern.Append(Regex.Escape(c.ToString()));
            }

            return pattern.Append('$').ToString();
        }

        private static CompareInfo CreateCompare()
        {
            try
            {
                return CultureInfo.GetCultureInfo("ko-KR").CompareInfo;
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture.CompareInfo;
            }
        }
    }
}
