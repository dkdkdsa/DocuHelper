using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    internal sealed class PptToolSchema
    {
        private readonly Dictionary<string, string> _types = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _choices = new(StringComparer.Ordinal);

        private PptToolSchema()
        {
        }

        public static PptToolSchema Parse(string json)
        {
            var schema = new PptToolSchema();
            try
            {
                using var document = JsonDocument.Parse(json ?? "{}");
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("properties", out var properties)
                    || properties.ValueKind != JsonValueKind.Object)
                    return schema;

                foreach (var property in properties.EnumerateObject())
                {
                    var type = property.Value.ValueKind == JsonValueKind.Object && property.Value.TryGetProperty("type", out var kind) && kind.ValueKind == JsonValueKind.String
                        ? kind.GetString()
                        : null;
                    schema._types[property.Name] = type;
                    if (property.Value.ValueKind == JsonValueKind.Object
                        && property.Value.TryGetProperty("enum", out var choices)
                        && choices.ValueKind == JsonValueKind.Array)
                    {
                        schema._choices[property.Name] = choices.EnumerateArray()
                            .Where(c => c.ValueKind == JsonValueKind.String)
                            .Select(c => c.GetString())
                            .ToHashSet(StringComparer.Ordinal);
                    }
                }
            }
            catch (JsonException)
            {
            }

            return schema;
        }

        public bool Has(string name) => _types.ContainsKey(name);

        public string TypeOf(string name) => _types.TryGetValue(name, out var type) ? type : null;

        public bool Allows(string name, string value) =>
            !_choices.TryGetValue(name, out var choices) || choices.Contains(value);
    }
}
