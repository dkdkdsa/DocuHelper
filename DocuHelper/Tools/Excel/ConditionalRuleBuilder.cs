using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public static class ConditionalRuleBuilder
    {
        public static readonly IReadOnlyList<string> RuleNames = new[]
        {
            "cell_value", "text_contains", "text_not_contains", "text_begins", "text_ends", "blanks", "no_blanks", "errors", "no_errors",
            "duplicate", "unique", "top", "bottom", "above_average", "below_average", "date", "data_bar", "color_scale", "icon_set", "formula",
        };

        private static readonly string[] s_formatKeys = { "fill", "font_color", "bold", "italic", "underline", "strike", "border_color" };

        private static readonly Regex s_reference = new(@"^(.+!)?\$?[A-Za-z]{1,3}\$?[0-9]{1,7}$", RegexOptions.CultureInvariant);

        public static ConditionalRuleDraft Build(ToolInput input, CellAddress anchor, Func<string, string, string> check)
        {
            var name = input.GetOptionalString("rule")?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(name))
                throw new ToolInputException($"어떤 규칙인지 rule 값으로 알려 주세요. 쓸 수 있는 규칙: {string.Join(", ", RuleNames)}");

            if (!RuleNames.Contains(name))
                throw new ToolInputException($"‘{name}’ 규칙은 없어요. 쓸 수 있는 규칙: {string.Join(", ", RuleNames)}");

            var cell = anchor.ToString();
            var rule = new ConditionalFormattingRule();
            var needsLook = true;
            var stoppable = true;
            string label;
            switch (name)
            {
                case "cell_value":
                    label = CellValue(input, rule, check);
                    break;
                case "text_contains":
                case "text_not_contains":
                case "text_begins":
                case "text_ends":
                    label = TextRule(input, rule, name, cell);
                    break;
                case "blanks":
                    rule.Type = ConditionalFormatValues.ContainsBlanks;
                    rule.Append(new Formula($"LEN(TRIM({cell}))=0"));
                    label = "blank cells";
                    break;
                case "no_blanks":
                    rule.Type = ConditionalFormatValues.NotContainsBlanks;
                    rule.Append(new Formula($"LEN(TRIM({cell}))>0"));
                    label = "non-blank cells";
                    break;
                case "errors":
                    rule.Type = ConditionalFormatValues.ContainsErrors;
                    rule.Append(new Formula($"ISERROR({cell})"));
                    label = "cells with errors";
                    break;
                case "no_errors":
                    rule.Type = ConditionalFormatValues.NotContainsErrors;
                    rule.Append(new Formula($"NOT(ISERROR({cell}))"));
                    label = "cells without errors";
                    break;
                case "duplicate":
                    rule.Type = ConditionalFormatValues.DuplicateValues;
                    label = "duplicate values";
                    break;
                case "unique":
                    rule.Type = ConditionalFormatValues.UniqueValues;
                    label = "unique values";
                    break;
                case "top":
                case "bottom":
                    label = TopBottom(input, rule, name == "bottom");
                    break;
                case "above_average":
                    rule.Type = ConditionalFormatValues.AboveAverage;
                    label = "values above the average";
                    break;
                case "below_average":
                    rule.Type = ConditionalFormatValues.AboveAverage;
                    rule.AboveAverage = false;
                    label = "values below the average";
                    break;
                case "date":
                    label = DateRule(input, rule, cell);
                    break;
                case "data_bar":
                    needsLook = false;
                    stoppable = false;
                    label = DataBar(input, rule);
                    break;
                case "color_scale":
                    needsLook = false;
                    stoppable = false;
                    label = ColorScale(input, rule);
                    break;
                case "icon_set":
                    needsLook = false;
                    label = IconSet(input, rule);
                    break;
                default:
                    label = FormulaRule(input, rule, check);
                    break;
            }

            ExcelDxfLook look = null;
            var defaultLook = false;
            if (needsLook)
            {
                look = ParseLook(input, out defaultLook);
                if (defaultLook)
                    look = ExcelDxfLook.Preset(ExcelDxfLook.PresetNames[0]);
            }

            string ignored = null;
            if (input.GetOptionalBool("stop_if_true") == true)
            {
                if (stoppable)
                    rule.StopIfTrue = true;
                else
                    ignored = "stop_if_true does not apply to data bars and color scales and was ignored.";
            }

            return new ConditionalRuleDraft(rule, look, label, defaultLook, ignored);
        }

        public static string ParseHex(string input, string what)
        {
            var value = input?.Trim().TrimStart('#');
            if (!ExcelColors.IsHex(value))
                throw new ToolInputException($"{what}은 \"FF0000\" 같은 여섯 자리 색 코드로 알려 주세요. 지금 값: {input}");

            return value.ToUpperInvariant();
        }

        public static bool IsRelative(ConditionalFormattingRule rule) =>
            Texts(rule).Any(t => FormulaText.Shift(t.Text, 1, 1) != t.Text);

        public static bool Shift(ConditionalFormattingRule rule, int rowDelta, int columnDelta)
        {
            if (rowDelta == 0 && columnDelta == 0)
                return true;

            var texts = Texts(rule).ToList();
            var shifted = new List<string>();
            foreach (var (_, text) in texts)
            {
                if (!FormulaText.TryShift(text, rowDelta, columnDelta, out var moved))
                    return false;

                shifted.Add(moved);
            }

            for (var i = 0; i < texts.Count; i++)
                texts[i].Apply(shifted[i]);

            return true;
        }

        public static string Quote(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";

        private static IEnumerable<(Action<string> Apply, string Text)> Texts(ConditionalFormattingRule rule)
        {
            foreach (var formula in rule.Descendants<Formula>().Where(f => !string.IsNullOrEmpty(f.Text)))
                yield return (text => formula.Text = text, formula.Text);

            foreach (var cfvo in rule.Descendants<ConditionalFormatValueObject>().Where(c => c.Type?.InnerText == "formula" && !string.IsNullOrEmpty(c.Val?.Value)))
                yield return (text => cfvo.Val = text, cfvo.Val.Value);
        }

        private static string CellValue(ToolInput input, ConditionalFormattingRule rule, Func<string, string, string> check)
        {
            var key = input.GetOptionalString("operator")?.Trim().ToLowerInvariant();
            var (op, words) = key switch
            {
                "greater_than" => (ConditionalFormattingOperatorValues.GreaterThan, "greater than"),
                "less_than" => (ConditionalFormattingOperatorValues.LessThan, "less than"),
                "greater_equal" => (ConditionalFormattingOperatorValues.GreaterThanOrEqual, "greater than or equal to"),
                "less_equal" => (ConditionalFormattingOperatorValues.LessThanOrEqual, "less than or equal to"),
                "equal" => (ConditionalFormattingOperatorValues.Equal, "equal to"),
                "not_equal" => (ConditionalFormattingOperatorValues.NotEqual, "not equal to"),
                "between" => (ConditionalFormattingOperatorValues.Between, "between"),
                "not_between" => (ConditionalFormattingOperatorValues.NotBetween, "not between"),
                _ => throw new ToolInputException("cell_value 규칙은 operator가 필요해요: greater_than, less_than, greater_equal, less_equal, equal, not_equal, between, not_between 중에서 알려 주세요."),
            };

            var first = Operand(input.GetOptionalString("value"), "value", check);
            rule.Type = ConditionalFormatValues.CellIs;
            rule.Operator = op;
            rule.Append(new Formula(FormulaText.ForStorage(first)));
            if (key is not ("between" or "not_between"))
                return $"cell value {words} {first}";

            var second = Operand(input.GetOptionalString("value2"), "value2", check);
            if (double.TryParse(first, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var low)
                && double.TryParse(second, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var high)
                && low > high)
                throw new ToolInputException("between은 작은 값을 value에, 큰 값을 value2에 알려 주세요.");

            rule.Append(new Formula(FormulaText.ForStorage(second)));
            return $"cell value {words} {first} and {second}";
        }

        private static string Operand(string raw, string field, Func<string, string, string> check)
        {
            var value = raw?.Trim();
            if (string.IsNullOrEmpty(value))
                throw new ToolInputException($"{field} 값이 필요해요. 숫자, 날짜(2026-06-30), 칸 주소($B$1)나 =수식으로 알려 주세요.");

            if (value[0] == '=')
                return check(FormulaText.Strip(value), $"{field}의");

            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                if (value.Substring(1, value.Length - 2).Replace("\"\"", string.Empty).Contains('"'))
                    throw new ToolInputException($"{field}의 글자 안에 큰따옴표를 쓰려면 \"\"처럼 두 번 써 주세요: {value}");

                return value;
            }

            if (s_reference.IsMatch(value))
                return check(value, $"{field}의");

            if (value.Contains(':') && CellRange.TryParse(value.Substring(value.LastIndexOf('!') + 1), out _))
                throw new ToolInputException($"{field}에는 칸 하나만 쓸 수 있어요. 범위 대신 칸 주소($B$1)나 숫자를 알려 주세요: {value}");

            var typed = TypedInput.Parse(value);
            return typed.Kind switch
            {
                TypedInputKind.Number => ExcelValue.Invariant(typed.Number),
                TypedInputKind.Boolean => typed.Number != 0 ? "TRUE" : "FALSE",
                _ => Quote(value),
            };
        }

        private static string TextRule(ToolInput input, ConditionalFormattingRule rule, string name, string cell)
        {
            var text = input.GetOptionalString("text");
            if (string.IsNullOrEmpty(text))
                throw new ToolInputException("text 값으로 찾을 글자를 알려 주세요.");

            if (text.Length > 255)
                throw new ToolInputException("찾을 글자는 255자까지 쓸 수 있어요.");

            var quoted = Quote(text);
            rule.Text = text;
            switch (name)
            {
                case "text_contains":
                    rule.Type = ConditionalFormatValues.ContainsText;
                    rule.Operator = ConditionalFormattingOperatorValues.ContainsText;
                    rule.Append(new Formula($"NOT(ISERROR(SEARCH({quoted},{cell})))"));
                    return $"text containing {quoted}";
                case "text_not_contains":
                    rule.Type = ConditionalFormatValues.NotContainsText;
                    rule.Operator = ConditionalFormattingOperatorValues.NotContains;
                    rule.Append(new Formula($"ISERROR(SEARCH({quoted},{cell}))"));
                    return $"text not containing {quoted}";
                case "text_begins":
                    rule.Type = ConditionalFormatValues.BeginsWith;
                    rule.Operator = ConditionalFormattingOperatorValues.BeginsWith;
                    rule.Append(new Formula($"LEFT({cell},LEN({quoted}))={quoted}"));
                    return $"text beginning with {quoted}";
                default:
                    rule.Type = ConditionalFormatValues.EndsWith;
                    rule.Operator = ConditionalFormattingOperatorValues.EndsWith;
                    rule.Append(new Formula($"RIGHT({cell},LEN({quoted}))={quoted}"));
                    return $"text ending with {quoted}";
            }
        }

        private static string TopBottom(ToolInput input, ConditionalFormattingRule rule, bool bottom)
        {
            var count = input.GetOptionalInt("count") ?? 10;
            var percent = input.GetOptionalBool("percent") == true;
            if (count < 1 || count > 1000)
                throw new ToolInputException("count는 1부터 1000 사이로 알려 주세요.");

            if (percent && count > 100)
                throw new ToolInputException("percent가 true이면 count는 1부터 100 사이(퍼센트)로 알려 주세요.");

            rule.Type = ConditionalFormatValues.Top10;
            rule.Rank = (uint)count;
            if (percent)
                rule.Percent = true;

            if (bottom)
                rule.Bottom = true;

            return $"{(bottom ? "bottom" : "top")} {count}{(percent ? "%" : " items")}";
        }

        private static string DateRule(ToolInput input, ConditionalFormattingRule rule, string cell)
        {
            var key = input.GetOptionalString("period")?.Trim().ToLowerInvariant();
            var (period, formula) = key switch
            {
                "today" => (TimePeriodValues.Today, $"FLOOR({cell},1)=TODAY()"),
                "yesterday" => (TimePeriodValues.Yesterday, $"FLOOR({cell},1)=TODAY()-1"),
                "tomorrow" => (TimePeriodValues.Tomorrow, $"FLOOR({cell},1)=TODAY()+1"),
                "last_7_days" => (TimePeriodValues.Last7Days, $"AND(TODAY()-FLOOR({cell},1)<=6,FLOOR({cell},1)<=TODAY())"),
                "this_week" => (TimePeriodValues.ThisWeek, $"AND(TODAY()-ROUNDDOWN({cell},0)<=WEEKDAY(TODAY())-1,ROUNDDOWN({cell},0)-TODAY()<=7-WEEKDAY(TODAY()))"),
                "last_week" => (TimePeriodValues.LastWeek, $"AND(TODAY()-ROUNDDOWN({cell},0)>=(WEEKDAY(TODAY())),TODAY()-ROUNDDOWN({cell},0)<(WEEKDAY(TODAY())+7))"),
                "next_week" => (TimePeriodValues.NextWeek, $"AND(ROUNDDOWN({cell},0)-TODAY()>(7-WEEKDAY(TODAY())),ROUNDDOWN({cell},0)-TODAY()<(15-WEEKDAY(TODAY())))"),
                "this_month" => (TimePeriodValues.ThisMonth, $"AND(MONTH({cell})=MONTH(TODAY()),YEAR({cell})=YEAR(TODAY()))"),
                "last_month" => (TimePeriodValues.LastMonth, $"AND(MONTH({cell})=MONTH(EDATE(TODAY(),0-1)),YEAR({cell})=YEAR(EDATE(TODAY(),0-1)))"),
                "next_month" => (TimePeriodValues.NextMonth, $"AND(MONTH({cell})=MONTH(EDATE(TODAY(),0+1)),YEAR({cell})=YEAR(EDATE(TODAY(),0+1)))"),
                _ => throw new ToolInputException("date 규칙은 period가 필요해요: today, yesterday, tomorrow, last_7_days, this_week, last_week, next_week, this_month, last_month, next_month 중에서 알려 주세요."),
            };

            rule.Type = ConditionalFormatValues.TimePeriod;
            rule.TimePeriod = period;
            rule.Append(new Formula(formula));
            return "dates " + key.Replace('_', ' ');
        }

        private static string FormulaRule(ToolInput input, ConditionalFormattingRule rule, Func<string, string, string> check)
        {
            var formula = FormulaText.Strip(input.GetOptionalString("formula") ?? string.Empty);
            if (formula.Length == 0)
                throw new ToolInputException("formula 규칙은 formula가 필요해요. 범위의 왼쪽 위 칸 기준으로 =$C2>1000000처럼 알려 주세요.");

            var checkedFormula = check(formula, "조건");
            rule.Type = ConditionalFormatValues.Expression;
            rule.Append(new Formula(FormulaText.ForStorage(checkedFormula)));
            return $"formula ={checkedFormula}";
        }

        private static string DataBar(ToolInput input, ConditionalFormattingRule rule)
        {
            var color = input.Has("bar_color") ? ParseHex(input.GetOptionalString("bar_color"), "막대 색") : "638EC6";
            rule.Type = ConditionalFormatValues.DataBar;
            rule.Append(new DataBar(
                new ConditionalFormatValueObject { Type = ConditionalFormatValueObjectValues.Min },
                new ConditionalFormatValueObject { Type = ConditionalFormatValueObjectValues.Max },
                new Color { Rgb = "FF" + color }));
            return $"data bars in {color}";
        }

        private static string ColorScale(ToolInput input, ConditionalFormattingRule rule)
        {
            var colors = input.GetStringList("colors", false).Select((c, i) => ParseHex(c, $"colors {i + 1}번째 색")).ToList();
            if (colors.Count is < 2 or > 3)
                throw new ToolInputException("color_scale은 colors에 색 코드 2개나 3개를 낮은 값부터 높은 값 순서로 알려 주세요. 예: [\"F8696B\", \"FFEB84\", \"63BE7B\"]");

            var scale = new ColorScale();
            scale.Append(new ConditionalFormatValueObject { Type = ConditionalFormatValueObjectValues.Min });
            if (colors.Count == 3)
                scale.Append(new ConditionalFormatValueObject { Type = ConditionalFormatValueObjectValues.Percentile, Val = "50" });

            scale.Append(new ConditionalFormatValueObject { Type = ConditionalFormatValueObjectValues.Max });
            foreach (var color in colors)
                scale.Append(new Color { Rgb = "FF" + color });

            rule.Type = ConditionalFormatValues.ColorScale;
            rule.Append(scale);
            return $"{colors.Count}-color scale {string.Join(" > ", colors)}";
        }

        private static string IconSet(ToolInput input, ConditionalFormattingRule rule)
        {
            var requested = input.GetOptionalString("icon_set");
            var name = ConditionalIconSets.Find(requested);
            if (name == null)
                throw new ToolInputException($"icon_set에는 아이콘 묶음 이름을 알려 주세요: {string.Join(", ", ConditionalIconSets.Names)}");

            var set = new IconSet();
            set.SetAttribute(new OpenXmlAttribute("iconSet", string.Empty, name));
            foreach (var percent in ConditionalIconSets.DefaultPercents(ConditionalIconSets.CountOf(name)))
                set.Append(new ConditionalFormatValueObject { Type = ConditionalFormatValueObjectValues.Percent, Val = percent.ToString(System.Globalization.CultureInfo.InvariantCulture) });

            var reverse = input.GetOptionalBool("reverse_icons") == true;
            if (reverse)
                set.Reverse = true;

            rule.Type = ConditionalFormatValues.IconSet;
            rule.Append(set);
            return $"icon set {name}{(reverse ? " (reversed)" : string.Empty)}";
        }

        private static ExcelDxfLook ParseLook(ToolInput input, out bool defaultLook)
        {
            ExcelDxfLook look = null;
            var preset = input.GetOptionalString("look")?.Trim();
            if (!string.IsNullOrEmpty(preset))
            {
                var key = preset.ToLowerInvariant();
                if (!ExcelDxfLook.PresetNames.Contains(key))
                    throw new ToolInputException($"look은 다음 중에서 알려 주세요: {string.Join(", ", ExcelDxfLook.PresetNames)}");

                look = ExcelDxfLook.Preset(key);
            }

            if (input.Has("format"))
            {
                var format = input.Root.GetProperty("format");
                if (format.ValueKind != JsonValueKind.Object)
                    throw new ToolInputException($"format은 {{fill, font_color, bold, italic, underline, strike, border_color}} 형태의 객체로 알려 주세요.");

                foreach (var property in format.EnumerateObject())
                {
                    if (!s_formatKeys.Contains(property.Name))
                        throw new ToolInputException($"format에 ‘{property.Name}’ 항목은 없어요. 쓸 수 있는 항목: {string.Join(", ", s_formatKeys)}");
                }

                look ??= new ExcelDxfLook();
                var fill = Text(format, "fill");
                if (fill != null)
                    look.Fill = ParseHex(fill, "format.fill");

                var color = Text(format, "font_color");
                if (color != null)
                    look.FontColor = ParseHex(color, "format.font_color");

                var border = Text(format, "border_color");
                if (border != null)
                    look.BorderColor = ParseHex(border, "format.border_color");

                look.Bold = Flag(format, "bold") ?? look.Bold;
                look.Italic = Flag(format, "italic") ?? look.Italic;
                look.Underline = Flag(format, "underline") ?? look.Underline;
                look.Strike = Flag(format, "strike") ?? look.Strike;
            }

            defaultLook = look == null || look.IsEmpty;
            return look;
        }

        private static string Text(JsonElement format, string name)
        {
            if (!format.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
                return null;

            return ToolInput.AsText(value);
        }

        private static bool? Flag(JsonElement format, string name)
        {
            if (!format.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
                return null;

            if (value.ValueKind == JsonValueKind.True)
                return true;

            if (value.ValueKind == JsonValueKind.False)
                return false;

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var flag))
                return flag;

            throw new ToolInputException($"format.{name} 값은 true 또는 false여야 해요.");
        }
    }
}
