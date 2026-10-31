using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public static class ValidationRules
    {
        public const int MaxTitle = 32;
        public const int MaxMessage = 255;
        public const int MaxListText = 255;
        public const double MaxDateSerial = 2958465;

        private static readonly Regex s_time = new(@"^(\d{1,2}):(\d{2})(?::(\d{2}))?$", RegexOptions.CultureInvariant);

        public static (DataValidation Element, string Words, string Notes) Build(ToolInput input, IReadOnlyList<CellRange> areas, Func<string, string, string> check, Func<string, bool> hasName)
        {
            var promptTitle = Limited(input, "input_title", MaxTitle, "입력 안내 제목");
            var prompt = Limited(input, "input_message", MaxMessage, "입력 안내 내용");
            var errorTitle = Limited(input, "error_title", MaxTitle, "오류 알림 제목");
            var error = Limited(input, "error_message", MaxMessage, "오류 알림 내용");
            var hasPrompt = promptTitle != null || prompt != null;
            var type = ParseType(input, hasPrompt);
            var style = ParseStyle(input);
            var multiple = areas.Count > 1;
            var element = new DataValidation { SequenceOfReferences = new ListValue<StringValue> { InnerText = SqrefMath.Format(areas) } };
            string words;
            if (type == "any")
            {
                if (!hasPrompt)
                    return (null, null, string.Empty);

                element.AllowBlank = true;
                element.ShowErrorMessage = true;
                words = "any value (only the input message is shown)";
            }
            else
            {
                if (input.GetOptionalBool("allow_blank") != false)
                    element.AllowBlank = true;

                element.ShowErrorMessage = true;
                if (style != "stop")
                    element.ErrorStyle = style == "warning" ? DataValidationErrorStyleValues.Warning : DataValidationErrorStyleValues.Information;

                words = type switch
                {
                    "list" => BuildList(input, element, check, hasName, multiple),
                    "custom" => BuildCustom(input, element, check, multiple),
                    _ => BuildNumeric(input, element, type, check, multiple),
                };
            }

            if (hasPrompt)
            {
                element.ShowInputMessage = true;
                if (promptTitle != null)
                    element.PromptTitle = promptTitle;

                if (prompt != null)
                    element.Prompt = prompt;
            }

            if (type != "any")
            {
                if (errorTitle != null)
                    element.ErrorTitle = errorTitle;

                if (error != null)
                    element.Error = error;
            }

            var notes = new List<string>();
            if (type != "any")
            {
                notes.Add(element.AllowBlank?.Value == true ? "empty cells are allowed" : "empty cells are not allowed");
                notes.Add($"invalid entries are answered with a {style} alert");
            }

            if (hasPrompt)
                notes.Add("an input message is shown when a cell is selected");

            return (element, words, string.Join("; ", notes));
        }

        private static string BuildList(ToolInput input, DataValidation element, Func<string, string, string> check, Func<string, bool> hasName, bool multiple)
        {
            var items = input.Has("items") ? input.GetStringList("items", false).Select(i => (i ?? string.Empty).Trim()).ToList() : new List<string>();
            var source = input.GetOptionalString("source");
            var hasSource = !string.IsNullOrWhiteSpace(source);
            if (items.Count > 0 && hasSource)
                throw new ToolInputException("items와 source는 함께 쓸 수 없어요. 직접 쓴 목록이면 items만, 셀 범위나 이름이면 source만 알려 주세요.");

            if (items.Count == 0 && !hasSource)
                throw new ToolInputException("목록에 넣을 값을 items로 알려 주거나, 셀 범위나 이름을 source로 알려 주세요. 예: items [\"예\", \"아니오\"] 또는 source \"=$A$2:$A$9\"");

            if (input.GetOptionalBool("in_cell_dropdown") == false)
                element.ShowDropDown = true;

            element.Type = DataValidationValues.List;
            if (hasSource)
            {
                var formula = ListSource(source, check, hasName, multiple);
                element.Formula1 = new Formula1(FormulaText.ForStorage(formula));
                return $"a dropdown list taken from ={formula}";
            }

            foreach (var item in items)
            {
                if (item.Length == 0)
                    throw new ToolInputException("items에 빈 값이 있어요. 비어 있지 않은 값만 알려 주세요.");

                if (item.Contains(','))
                    throw new ToolInputException($"‘{item}’ 항목에 쉼표가 들어 있어요. 항목 안에는 쉼표를 쓸 수 없으니 쉼표를 빼거나, 값을 셀에 적고 source로 그 범위를 알려 주세요.");
            }

            var joined = string.Join(",", items);
            if (joined.Length > MaxListText)
                throw new ToolInputException($"items가 너무 길어요. 쉼표를 포함해 {MaxListText}자 이하여야 하는데 {joined.Length}자예요. 값을 셀에 적고 source로 그 범위를 알려 주세요.");

            element.Formula1 = new Formula1("\"" + joined.Replace("\"", "\"\"") + "\"");
            return $"a dropdown list of {string.Join(", ", items.Select(i => "‘" + i + "’"))}";
        }

        private static string ListSource(string source, Func<string, string, string> check, Func<string, bool> hasName, bool multiple)
        {
            var stripped = FormulaText.Strip(source);
            if (stripped.Length == 0)
                throw new ToolInputException("source가 비어 있어요. =$A$2:$A$9나 =거래처처럼 알려 주세요.");

            var formula = check(stripped, "목록 원본");
            var node = FormulaParser.Parse(formula);
            switch (node.Kind)
            {
                case FormulaNodeKind.Reference:
                    var area = node.Reference.ToFullRange();
                    if (area.ColumnCount > 1 && area.RowCount > 1)
                        throw new ToolInputException($"목록 원본은 한 열 또는 한 행이어야 해요. {area}처럼 여러 열과 행에 걸친 범위는 쓸 수 없으니 한 줄로 알려 주세요.");

                    break;
                case FormulaNodeKind.Name:
                    if (!hasName(node.Value))
                        throw new ToolInputException($"‘{node.Value}’ 이름을 찾을 수 없어요. 셀 범위(=$A$2:$A$9)나 정의된 이름으로 알려 주세요.");

                    break;
                case FormulaNodeKind.Function:
                    break;
                default:
                    throw new ToolInputException("source에는 셀 범위나 이름을 알려 주세요. 값을 직접 쓰려면 items를 사용하세요.");
            }

            RejectRelative(formula, multiple);
            return formula;
        }

        private static string BuildCustom(ToolInput input, DataValidation element, Func<string, string, string> check, bool multiple)
        {
            var text = input.GetOptionalString("formula");
            if (string.IsNullOrWhiteSpace(text))
                throw new ToolInputException("custom 규칙에는 참이어야 하는 수식을 formula로 알려 주세요. 예: =COUNTIF($A:$A,A2)=1");

            var formula = check(FormulaText.Strip(text), "입력 제한");
            RejectRelative(formula, multiple);
            element.Type = DataValidationValues.Custom;
            element.Formula1 = new Formula1(FormulaText.ForStorage(formula));
            return $"values for which ={formula} is true (written for the top-left cell of the range)";
        }

        private static string BuildNumeric(ToolInput input, DataValidation element, string type, Func<string, string, string> check, bool multiple)
        {
            var op = ParseOperator(input.GetOptionalString("operator"));
            var two = op == DataValidationOperatorValues.Between || op == DataValidationOperatorValues.NotBetween;
            var first = Operand(input, "value1", type, check, multiple);
            (string Formula, string Words, double? Number) second = default;
            if (two)
            {
                if (string.IsNullOrWhiteSpace(input.GetOptionalString("value2")))
                    throw new ToolInputException("between과 not_between에는 value1(최솟값)과 value2(최댓값)를 모두 알려 주세요.");

                second = Operand(input, "value2", type, check, multiple);
                if (first.Number.HasValue && second.Number.HasValue && first.Number.Value > second.Number.Value)
                    throw new ToolInputException($"최솟값({first.Words})이 최댓값({second.Words})보다 커요. value1이 작은 값, value2가 큰 값이 되도록 알려 주세요.");
            }

            element.Type = type switch
            {
                "whole" => DataValidationValues.Whole,
                "decimal" => DataValidationValues.Decimal,
                "date" => DataValidationValues.Date,
                "time" => DataValidationValues.Time,
                _ => DataValidationValues.TextLength,
            };
            if (op != DataValidationOperatorValues.Between)
                element.Operator = op;

            element.Formula1 = new Formula1(first.Formula);
            if (two)
                element.Formula2 = new Formula2(second.Formula);

            var noun = type switch
            {
                "whole" => "whole numbers",
                "decimal" => "decimal numbers",
                "date" => "dates",
                "time" => "times",
                _ => "text length",
            };
            return $"{noun} {Phrase(op, first.Words, second.Words)}";
        }

        private static (string Formula, string Words, double? Number) Operand(ToolInput input, string field, string type, Func<string, string, string> check, bool multiple)
        {
            var text = input.GetOptionalString(field)?.Trim();
            if (string.IsNullOrEmpty(text))
                throw new ToolInputException($"{field}에 기준값을 알려 주세요. 숫자, 날짜(2026-06-30), 시간(09:00) 또는 =로 시작하는 수식이나 셀 참조예요.");

            if (text[0] == '=')
            {
                var formula = check(FormulaText.Strip(text), "입력 제한");
                RejectRelative(formula, multiple);
                return (FormulaText.ForStorage(formula), "=" + formula, null);
            }

            var number = type switch
            {
                "date" => ParseDate(text, field),
                "time" => ParseTime(text, field),
                _ => ParsePlain(text, type, field),
            };
            var words = type switch
            {
                "date" => ExcelNumberFormat.ToDateTime(number).ToString("yyyy-MM-dd"),
                "time" => ExcelNumberFormat.ToDateTime(number).ToString("HH:mm"),
                _ => ExcelValue.Invariant(number),
            };
            return (ExcelValue.Invariant(number), words, number);
        }

        private static double ParsePlain(string text, string type, string field)
        {
            if (!TypedInput.TryParseNumber(text, out var number, out _))
                throw new ToolInputException($"{field}의 ‘{text}’은(는) 숫자로 알아볼 수 없어요. 숫자나 =로 시작하는 수식으로 알려 주세요.");

            if (type is "whole" or "text_length" && Math.Abs(number - Math.Round(number)) > 1e-9)
                throw new ToolInputException($"{field}의 ‘{text}’은(는) 정수가 아니에요. {(type == "whole" ? "정수만 허용하는 규칙이니 정수로 알려 주세요. 소수를 허용하려면 type을 decimal로 바꿔 주세요." : "글자 수는 정수로 알려 주세요.")}");

            if (type == "text_length" && number < 0)
                throw new ToolInputException($"{field}의 글자 수는 0 이상이어야 해요.");

            return type is "whole" or "text_length" ? Math.Round(number) : number;
        }

        private static double ParseDate(string text, string field)
        {
            if (!TypedInput.TryParseNumber(text, out var number, out var hint) || hint is TypedInput.PercentHint || number < 0 || number > MaxDateSerial)
                throw new ToolInputException($"{field}의 ‘{text}’은(는) 날짜로 알아볼 수 없어요. 2026-06-30처럼 알려 주세요.");

            return number;
        }

        private static double ParseTime(string text, string field)
        {
            var match = s_time.Match(text);
            if (match.Success)
            {
                var hours = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                var minutes = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                var seconds = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
                if (hours < 24 && minutes < 60 && seconds < 60)
                    return (hours * 3600 + minutes * 60 + seconds) / 86400d;
            }
            else if (TypedInput.TryParseNumber(text, out var number, out var hint) && hint == null && number >= 0 && number < 1)
            {
                return number;
            }

            throw new ToolInputException($"{field}의 ‘{text}’은(는) 시간으로 알아볼 수 없어요. 09:00이나 18:30처럼 알려 주세요.");
        }

        private static string Phrase(DataValidationOperatorValues op, string first, string second)
        {
            if (op == DataValidationOperatorValues.Between)
                return $"between {first} and {second}";

            if (op == DataValidationOperatorValues.NotBetween)
                return $"not between {first} and {second}";

            if (op == DataValidationOperatorValues.Equal)
                return $"equal to {first}";

            if (op == DataValidationOperatorValues.NotEqual)
                return $"not equal to {first}";

            if (op == DataValidationOperatorValues.GreaterThan)
                return $"greater than {first}";

            if (op == DataValidationOperatorValues.LessThan)
                return $"less than {first}";

            return op == DataValidationOperatorValues.GreaterThanOrEqual ? $"greater than or equal to {first}" : $"less than or equal to {first}";
        }

        private static DataValidationOperatorValues ParseOperator(string text)
        {
            switch ((text ?? "between").Trim().ToLowerInvariant().Replace('-', '_'))
            {
                case "":
                case "between":
                    return DataValidationOperatorValues.Between;
                case "not_between":
                    return DataValidationOperatorValues.NotBetween;
                case "equal":
                    return DataValidationOperatorValues.Equal;
                case "not_equal":
                    return DataValidationOperatorValues.NotEqual;
                case "greater_than":
                    return DataValidationOperatorValues.GreaterThan;
                case "less_than":
                    return DataValidationOperatorValues.LessThan;
                case "greater_equal":
                    return DataValidationOperatorValues.GreaterThanOrEqual;
                case "less_equal":
                    return DataValidationOperatorValues.LessThanOrEqual;
                default:
                    throw new ToolInputException("operator는 between, not_between, equal, not_equal, greater_than, less_than, greater_equal, less_equal 중에서 알려 주세요.");
            }
        }

        private static string ParseType(ToolInput input, bool hasPrompt)
        {
            var text = input.GetOptionalString("type");
            if (string.IsNullOrWhiteSpace(text))
            {
                if (hasPrompt)
                    return "any";

                throw new ToolInputException("어떤 값을 허용할지 type으로 알려 주세요: list, whole, decimal, date, time, text_length, custom, any 중 하나예요.");
            }

            var type = text.Trim().ToLowerInvariant().Replace('-', '_');
            if (type is "any" or "whole" or "decimal" or "list" or "date" or "time" or "text_length" or "custom")
                return type;

            throw new ToolInputException($"type ‘{text.Trim()}’은(는) 쓸 수 없어요. list, whole, decimal, date, time, text_length, custom, any 중에서 알려 주세요.");
        }

        private static string ParseStyle(ToolInput input)
        {
            var text = input.GetOptionalString("error_style");
            if (string.IsNullOrWhiteSpace(text))
                return "stop";

            var style = text.Trim().ToLowerInvariant();
            if (style is "stop" or "warning" or "information")
                return style;

            throw new ToolInputException("error_style은 stop, warning, information 중에서 알려 주세요.");
        }

        private static string Limited(ToolInput input, string field, int limit, string label)
        {
            var text = input.GetOptionalString(field);
            if (string.IsNullOrEmpty(text))
                return null;

            if (text.Length > limit)
                throw new ToolInputException($"{label}은(는) {limit}자까지만 쓸 수 있는데 {text.Length}자예요. 줄여서 다시 알려 주세요.");

            return text;
        }

        private static void RejectRelative(string formula, bool multiple)
        {
            if (!multiple || !FormulaText.References(formula).Any(IsRelative))
                return;

            throw new ToolInputException("범위가 여러 곳이면 칸을 따라 움직이는 상대 참조(예: A2)를 쓸 수 없어요. $A$2처럼 고정하거나, 범위를 하나씩 따로 알려 주세요.");
        }

        private static bool IsRelative(FormulaReference reference) => reference.Kind switch
        {
            FormulaReferenceKind.Cell => !reference.Column1Absolute || !reference.Row1Absolute,
            FormulaReferenceKind.Area => !reference.Column1Absolute || !reference.Row1Absolute || !reference.Column2Absolute || !reference.Row2Absolute,
            FormulaReferenceKind.Columns => !reference.Column1Absolute || !reference.Column2Absolute,
            _ => !reference.Row1Absolute || !reference.Row2Absolute,
        };
    }
}
