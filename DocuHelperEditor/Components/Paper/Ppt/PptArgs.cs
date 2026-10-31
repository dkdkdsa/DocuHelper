using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    internal static partial class PptArgs
    {
        [GeneratedRegex(@"\{(\w+)\}")]
        private static partial Regex Placeholder();

        public static bool IsPlaceholder(string text) => text != null && Placeholder().IsMatch(text);

        public static JsonObject ParseObject(string json) =>
            string.IsNullOrWhiteSpace(json) ? new JsonObject() : JsonNode.Parse(json) as JsonObject ?? new JsonObject();

        public static JsonObject Merge(JsonObject baseArgs, JsonObject over)
        {
            var merged = new JsonObject();
            foreach (var pair in baseArgs)
                merged[pair.Key] = pair.Value?.DeepClone();

            foreach (var pair in over)
                merged[pair.Key] = pair.Value?.DeepClone();

            return merged;
        }

        public static JsonObject Resolve(JsonObject template, Func<string, JsonNode> lookup)
        {
            return ResolveNode(template, lookup) as JsonObject ?? new JsonObject();
        }

        public static string Fill(string text, Func<string, JsonNode> lookup)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return Placeholder().Replace(text, match => TextOf(lookup(match.Groups[1].Value)));
        }

        private static JsonNode ResolveNode(JsonNode node, Func<string, JsonNode> lookup)
        {
            switch (node)
            {
                case null:
                    return null;
                case JsonObject obj:
                    var resolved = new JsonObject();
                    foreach (var pair in obj)
                    {
                        var value = ResolveNode(pair.Value, lookup);
                        if (value != null)
                            resolved[pair.Key] = value;
                    }

                    return resolved.Count == 0 && obj.Count > 0 ? null : resolved;
                case JsonArray array:
                    var items = new JsonArray();
                    foreach (var item in array)
                    {
                        var value = ResolveNode(item, lookup);
                        if (value != null)
                            items.Add(value);
                    }

                    return items.Count == 0 && array.Count > 0 ? null : items;
                case JsonValue value when value.TryGetValue<string>(out var text):
                    var whole = Placeholder().Match(text);
                    if (whole.Success && whole.Length == text.Length)
                        return Clean(lookup(whole.Groups[1].Value));

                    return JsonValue.Create(Fill(text, lookup));
                default:
                    return node.DeepClone();
            }
        }

        private static JsonNode Clean(JsonNode value)
        {
            if (value is JsonValue single && single.TryGetValue<string>(out var text) && string.IsNullOrEmpty(text))
                return null;

            if (value is JsonArray array && array.Count == 0)
                return null;

            return value?.DeepClone();
        }

        private static string TextOf(JsonNode node)
        {
            if (node == null)
                return string.Empty;

            if (node is JsonValue value)
            {
                if (value.TryGetValue<string>(out var text))
                    return text;

                if (value.TryGetValue<double>(out var number))
                    return number.ToString("0.##", CultureInfo.InvariantCulture);
            }

            return node.ToJsonString();
        }
    }
}
