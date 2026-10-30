using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace DocuHelper.Tools.Ppt
{
    public class PptLine
    {
        private const int MaxLines = 200;
        private const int MaxCharacters = 20000;

        public PptLine(string text, int? level)
        {
            Text = text ?? string.Empty;
            Level = level;
        }

        public string Text { get; }

        public int? Level { get; }

        public static IReadOnlyList<PptLine> Parse(ToolInput input, string name, bool required)
        {
            if (!input.Has(name))
            {
                if (required)
                    throw new ToolInputException($"'{name}' 값이 필요해요.");

                return Array.Empty<PptLine>();
            }

            var value = input.Get(name);
            var lines = new List<PptLine>();
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    lines.AddRange(FromText(value.GetString()));
                    break;
                case JsonValueKind.Array:
                    foreach (var item in value.EnumerateArray())
                        lines.Add(FromItem(item, name));

                    break;
                default:
                    throw new ToolInputException($"'{name}' 값은 글 목록이어야 해요.");
            }

            if (lines.Count > MaxLines)
                throw new ToolInputException($"한 번에 {MaxLines}줄까지만 넣을 수 있어요.");

            if (lines.Sum(l => l.Text.Length) > MaxCharacters)
                throw new ToolInputException("글이 너무 길어요. 나눠서 넣어 주세요.");

            return lines;
        }

        public static IReadOnlyList<PptLine> FromText(string text)
        {
            var parts = PptText.Clean(text).Split('\n').Select(p => p.Trim()).ToList();
            while (parts.Count > 0 && parts[^1].Length == 0)
                parts.RemoveAt(parts.Count - 1);

            return parts.Select(p => new PptLine(p, null)).ToList();
        }

        private static PptLine FromItem(JsonElement item, string name)
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.Object:
                    if (!item.TryGetProperty("text", out var text))
                        throw new ToolInputException($"'{name}'의 각 항목에는 'text' 값이 있어야 해요.");

                    int? level = null;
                    if (item.TryGetProperty("level", out var levelValue) && levelValue.ValueKind != JsonValueKind.Null)
                    {
                        if (!TryGetInt(levelValue, out var number) || number < 1 || number > 9)
                            throw new ToolInputException("'level' 값은 1~9 사이의 정수여야 해요.");

                        level = number - 1;
                    }

                    return new PptLine(Normalize(ToolInput.AsText(text)), level);
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return new PptLine(string.Empty, null);
                default:
                    return new PptLine(Normalize(ToolInput.AsText(item)), null);
            }
        }

        private static string Normalize(string text) => PptText.Clean(text).Trim();

        private static bool TryGetInt(JsonElement value, out int number)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out number))
                return true;

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return true;

            number = 0;
            return false;
        }
    }
}
