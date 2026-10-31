using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;

namespace DocuHelperEditor.State.Ribbon
{
    internal static class WordArgs
    {
        private static readonly JsonNode s_missing = JsonValue.Create("\u0000missing");

        private static readonly HashSet<string> s_numeric = new HashSet<string>(StringComparer.Ordinal)
        {
            "size",
            "width_pt",
            "indent_left_cm",
            "indent_right_cm",
            "space_before_pt",
            "space_after_pt",
            "outline_level",
            "start_at",
            "page",
            "zoom",
            "level",
        };

        public delegate bool Lookup(string name, out JsonNode value);

        public static JsonNode Parse(string json) => string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);

        public static JsonNode Merge(JsonNode template, JsonNode over)
        {
            if (template is JsonObject baseObject && over is JsonObject overObject)
            {
                var merged = new JsonObject();
                foreach (var pair in baseObject)
                    merged[pair.Key] = pair.Value?.DeepClone();

                foreach (var pair in overObject)
                {
                    if (merged[pair.Key] is JsonObject existing && pair.Value is JsonObject incoming)
                        merged[pair.Key] = Merge(existing, incoming);
                    else
                        merged[pair.Key] = pair.Value?.DeepClone();
                }

                return merged;
            }

            return over?.DeepClone();
        }

        public static JsonNode Resolve(JsonNode template, Lookup lookup, ICollection<string> missing)
        {
            var result = Walk(template, null, lookup, missing);
            return ReferenceEquals(result, s_missing) ? null : result;
        }

        public static IReadOnlyList<string> Placeholders(JsonNode template)
        {
            var names = new List<string>();
            Collect(template, names);
            return names;
        }

        public static bool IsPlaceholder(string text, out string name)
        {
            name = null;
            if (text == null || text.Length < 3 || text[0] != '{' || text[text.Length - 1] != '}')
                return false;

            name = text.Substring(1, text.Length - 2).Trim();
            return name.Length > 0;
        }

        private static void Collect(JsonNode node, ICollection<string> names)
        {
            switch (node)
            {
                case JsonObject o:
                    foreach (var pair in o)
                        Collect(pair.Value, names);

                    break;
                case JsonArray a:
                    foreach (var item in a)
                        Collect(item, names);

                    break;
                case JsonValue v when v.TryGetValue<string>(out var text):
                    if (IsPlaceholder(text, out var name))
                        names.Add(name);

                    break;
            }
        }

        private static JsonNode Walk(JsonNode node, string key, Lookup lookup, ICollection<string> missing)
        {
            switch (node)
            {
                case null:
                    return null;
                case JsonObject o:
                {
                    var copy = new JsonObject();
                    foreach (var pair in o)
                    {
                        var value = Walk(pair.Value, pair.Key, lookup, missing);
                        if (!ReferenceEquals(value, s_missing))
                            copy[pair.Key] = value;
                    }

                    return o.Count > 0 && copy.Count == 0 ? s_missing : copy;
                }

                case JsonArray a:
                {
                    var copy = new JsonArray();
                    foreach (var item in a)
                    {
                        var value = Walk(item, null, lookup, missing);
                        if (!ReferenceEquals(value, s_missing))
                            copy.Add(value);
                    }

                    return copy;
                }

                case JsonValue v when v.TryGetValue<string>(out var text) && IsPlaceholder(text, out var name):
                {
                    if (!Find(name, lookup, out var found))
                    {
                        var choice = key != null && name.Contains('|');
                        if (choice && lookup(key, out var byKey))
                            return Coerce(byKey, key);

                        missing?.Add(name);
                        if (choice)
                            missing?.Add(key);

                        return s_missing;
                    }

                    return Coerce(found, key);
                }

                default:
                    return node.DeepClone();
            }
        }

        private static bool Find(string name, Lookup lookup, out JsonNode value)
        {
            var question = name.IndexOf('?');
            var colon = name.IndexOf(':');
            if (question > 0 && colon > question)
            {
                var condition = name.Substring(0, question).Trim();
                if (lookup(condition, out var flag))
                {
                    var on = flag is JsonValue v && v.TryGetValue<bool>(out var b) && b;
                    value = JsonValue.Create((on ? name.Substring(question + 1, colon - question - 1) : name.Substring(colon + 1)).Trim());
                    return true;
                }

                value = null;
                return false;
            }

            return lookup(name, out value);
        }

        private static JsonNode Coerce(JsonNode value, string key)
        {
            if (key != null && s_numeric.Contains(key) && value is JsonValue v && v.TryGetValue<string>(out var text)
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return Math.Abs(number % 1) < 1e-9 ? JsonValue.Create((long)number) : JsonValue.Create(number);
            }

            return value?.DeepClone();
        }
    }
}
