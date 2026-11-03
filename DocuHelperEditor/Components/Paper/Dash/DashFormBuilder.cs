using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal sealed class DashFormBuilder
    {
        private static readonly string[] s_none = Array.Empty<string>();

        private readonly List<DialogField> _fields = new();
        private readonly Dictionary<string, JsonNode> _defaults = new(StringComparer.Ordinal);

        public IReadOnlyList<DialogField> Fields => _fields;

        public IReadOnlyDictionary<string, JsonNode> Defaults => _defaults;

        public static string TextOf(IReadOnlyDictionary<string, JsonNode> values, string key) =>
            values.TryGetValue(key, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;

        public static double? NumberOf(IReadOnlyDictionary<string, JsonNode> values, string key) =>
            values.TryGetValue(key, out var node) && node is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;

        public static bool FlagOf(IReadOnlyDictionary<string, JsonNode> values, string key) =>
            values.TryGetValue(key, out var node) && node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

        public DashFormBuilder Text(string name, string label, string value)
        {
            _fields.Add(new DialogField(name, label, "text", null, s_none, s_none));
            _defaults[name] = JsonValue.Create(value ?? string.Empty);
            return this;
        }

        public DashFormBuilder Number(string name, string label, double? value)
        {
            _fields.Add(new DialogField(name, label, "number", null, s_none, s_none));
            if (value.HasValue)
                _defaults[name] = JsonValue.Create(value.Value);

            return this;
        }

        public DashFormBuilder Flag(string name, string label, bool value)
        {
            _fields.Add(new DialogField(name, label, "bool", null, s_none, s_none));
            _defaults[name] = JsonValue.Create(value);
            return this;
        }

        public DashFormBuilder Choice(string name, string label, string value, IEnumerable<(string Value, string Label)> options)
        {
            var list = options.ToList();
            _fields.Add(new DialogField(name, label, "choice", null, list.Select(option => option.Value).ToList(), list.Select(option => option.Label).ToList()));
            _defaults[name] = JsonValue.Create(value ?? string.Empty);
            return this;
        }
    }
}
