using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DocuHelperEditor.State.Ribbon
{
    public static class DialogFields
    {
        internal static readonly string[] s_none = Array.Empty<string>();

        internal static string Key(string commandId, string dialogName) => commandId + "|" + dialogName;

        internal static DialogField Text(string name, string label, string value) =>
            new(name, label, "text", JsonSerializer.Serialize(value), s_none, s_none);

        internal static DialogField Number(string name, string label, string value) =>
            new(name, label, "number", value, s_none, s_none);

        internal static DialogField Flag(string name, string label, string value) =>
            new(name, label, "bool", value, s_none, s_none);

        internal static DialogField Color(string name, string label, string value) =>
            new(name, label, "color", JsonSerializer.Serialize(value), s_none, s_none);

        internal static DialogField Choice(string name, string label, string value, IReadOnlyList<string> options, IReadOnlyList<string> labels) =>
            new(name, label, "choice", JsonSerializer.Serialize(value), options, labels);

        internal static string Text(IReadOnlyDictionary<string, JsonNode> values, string name) =>
            values.TryGetValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;

        internal static double Num(IReadOnlyDictionary<string, JsonNode> values, string name) =>
            values.TryGetValue(name, out var node) && node is JsonValue value && value.TryGetValue<double>(out var number) ? number : 0;

        internal static int Count(IReadOnlyDictionary<string, JsonNode> values, string name) => (int)Num(values, name);

        internal static bool Flag(IReadOnlyDictionary<string, JsonNode> values, string name) =>
            values.TryGetValue(name, out var node) && node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

        internal static string Range(IReadOnlyDictionary<string, JsonNode> values, string name, double min, double max, string label)
        {
            var number = Num(values, name);
            return number < min || number > max
                ? $"{label}은(는) {min.ToString(CultureInfo.InvariantCulture)}~{max.ToString(CultureInfo.InvariantCulture)} 사이로 써 주세요."
                : null;
        }

        internal static string Length(string text, int min, int max) =>
            text.Length < min || text.Length > max ? $"글은 {min}~{max}자로 써 주세요." : null;

        internal static bool Link(string url) =>
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

        internal static string Tokens(string text) =>
            text.Replace("{쪽}", "{page}", StringComparison.Ordinal)
                .Replace("{전체쪽}", "{pages}", StringComparison.Ordinal)
                .Replace("{날짜}", "{date}", StringComparison.Ordinal);

        internal static string SymbolOf(string text)
        {
            var trimmed = text.Trim();
            if (trimmed.Length == 0)
                return null;

            if (trimmed.Length == 1 || (trimmed.Length == 2 && char.IsSurrogatePair(trimmed, 0)))
                return trimmed;

            var prefixed = trimmed.StartsWith("U+", StringComparison.OrdinalIgnoreCase);
            var hex = prefixed ? trimmed.Substring(2) : trimmed;
            if (hex.Length == 0 || hex.Length > 6 || (!prefixed && hex.Length < 4) || !hex.All(Uri.IsHexDigit))
                return null;

            var code = Convert.ToInt32(hex, 16);
            if (code < 0x20 || code >= 0x110000 || (code >= 0xD800 && code <= 0xDFFF))
                return null;

            var symbol = char.ConvertFromUtf32(code);
            return char.IsControl(symbol, 0) ? null : symbol;
        }
    }
}
