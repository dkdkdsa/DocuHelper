using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace DocuHelper.Tools.Ppt
{
    public static class PptInputs
    {
        public const double MaxCm = 1000;

        public static string Normalize(string token) =>
            (token ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');

        public static bool? Bool(ToolInput input, string name) => input.GetOptionalBool(name);

        public static double? Number(ToolInput input, string name, double min, double max)
        {
            if (!input.Has(name))
                return null;

            var value = input.GetOptionalNumber(name);
            if (value == null || !double.IsFinite(value.Value) || value.Value < min || value.Value > max)
                throw new ToolInputException($"'{name}' 값은 {Format(min)}~{Format(max)} 사이의 숫자여야 해요.");

            return value;
        }

        public static double RequiredNumber(ToolInput input, string name, double min, double max) =>
            Number(input, name, min, max) ?? throw new ToolInputException($"'{name}' 값이 필요해요.");

        public static int? Integer(ToolInput input, string name, int min, int max)
        {
            if (!input.Has(name))
                return null;

            if (!TryInteger(input.Get(name), out var number))
                throw new ToolInputException($"'{name}' 값은 정수여야 해요.");

            if (number < min || number > max)
                throw new ToolInputException($"'{name}' 값은 {min}~{max} 사이여야 해요.");

            return number;
        }

        public static int RequiredInteger(ToolInput input, string name, int min, int max) =>
            Integer(input, name, min, max) ?? throw new ToolInputException($"'{name}' 값이 필요해요.");

        public static double? Cm(ToolInput input, string name, double min = -MaxCm, double max = MaxCm)
        {
            if (!input.Has(name))
                return null;

            var value = input.GetOptionalNumber(name);
            if (value == null || !double.IsFinite(value.Value) || value.Value < min || value.Value > max)
                throw new ToolInputException($"'{name}' 값은 cm 단위로 {Format(min)}~{Format(max)} 사이여야 해요.");

            return value;
        }

        public static long? Emu(ToolInput input, string name, double minCm = -MaxCm, double maxCm = MaxCm)
        {
            var cm = Cm(input, name, minCm, maxCm);
            return cm == null ? null : PptUnits.ToEmu(cm.Value);
        }

        public static PptColorSpec Color(ToolInput input, string name, bool allowNone = false)
        {
            if (!input.Has(name))
                return null;

            var text = input.GetOptionalString(name);
            if (!PptColor.TryParse(text, allowNone, out var spec))
                throw new ToolInputException($"'{name}' 값을 색으로 읽지 못했어요. {PptColor.InvalidMessage}");

            return spec;
        }

        public static T? Enum<T>(ToolInput input, string name, params (string Tokens, T Value)[] choices)
            where T : struct
        {
            if (!input.Has(name))
                return null;

            var text = Normalize(input.GetOptionalString(name));
            foreach (var choice in choices)
            {
                if (choice.Tokens.Split('|').Any(t => Normalize(t) == text))
                    return choice.Value;
            }

            var names = string.Join(", ", choices.Select(c => c.Tokens.Split('|')[0]));
            throw new ToolInputException($"'{name}' 값은 {names} 중 하나여야 해요.");
        }

        public static string Choice(ToolInput input, string name, params string[] tokens)
        {
            if (!input.Has(name))
                return null;

            var text = Normalize(input.GetOptionalString(name));
            foreach (var token in tokens)
            {
                if (token.Split('|').Any(t => Normalize(t) == text))
                    return token.Split('|')[0];
            }

            var names = string.Join(", ", tokens.Select(t => t.Split('|')[0]));
            throw new ToolInputException($"'{name}' 값은 {names} 중 하나여야 해요.");
        }

        public static string Text(ToolInput input, string name, int maxLength)
        {
            if (!input.Has(name))
                return null;

            var text = PptText.Clean(input.GetOptionalString(name) ?? string.Empty).Trim();
            if (text.Length > maxLength)
                throw new ToolInputException($"'{name}' 값이 너무 길어요. {maxLength.ToString(CultureInfo.InvariantCulture)}자 안으로 줄여 주세요.");

            return text;
        }

        public static List<int> IntegerList(ToolInput input, string name, int min, int max, int maxCount)
        {
            if (!input.Has(name))
                return null;

            var value = input.Get(name);
            var numbers = new List<int>();
            switch (value.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var item in value.EnumerateArray())
                    {
                        if (!TryInteger(item, out var number))
                            throw new ToolInputException($"'{name}' 값은 정수 목록이어야 해요.");

                        numbers.Add(number);
                    }

                    break;
                case JsonValueKind.String:
                    foreach (var part in (value.GetString() ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                            throw new ToolInputException($"'{name}' 값은 정수 목록이어야 해요.");

                        numbers.Add(number);
                    }

                    break;
                default:
                    if (!TryInteger(value, out var single))
                        throw new ToolInputException($"'{name}' 값은 정수 목록이어야 해요.");

                    numbers.Add(single);
                    break;
            }

            if (numbers.Count == 0)
                throw new ToolInputException($"'{name}' 값에 번호를 하나 이상 알려 주세요.");

            if (numbers.Count > maxCount)
                throw new ToolInputException($"'{name}' 값은 {maxCount.ToString(CultureInfo.InvariantCulture)}개까지만 쓸 수 있어요.");

            if (numbers.Any(n => n < min || n > max))
                throw new ToolInputException($"'{name}' 값은 {min}~{max} 사이의 번호여야 해요.");

            return numbers.Distinct().OrderBy(n => n).ToList();
        }

        public static ToolInput Object(ToolInput input, string name)
        {
            if (!input.Has(name))
                return null;

            var value = input.Get(name);
            if (value.ValueKind != JsonValueKind.Object)
                throw new ToolInputException($"'{name}' 값은 항목이 있는 객체여야 해요.");

            return new ToolInput(value);
        }

        public static bool TryInteger(JsonElement value, out int number)
        {
            number = 0;
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    if (value.TryGetInt32(out number))
                        return true;

                    var real = value.GetDouble();
                    if (double.IsFinite(real) && Math.Abs(real) < int.MaxValue && real == Math.Floor(real))
                    {
                        number = (int)real;
                        return true;
                    }

                    return false;
                case JsonValueKind.String:
                    return int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
                default:
                    return false;
            }
        }

        private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
