using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public sealed class PptDialogPresenter : IPptDialogHost
    {
        private static readonly IReadOnlyDictionary<string, JsonNode> s_none = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

        private WordDialogRequest _active;

        public event Action<WordDialogRequest> Requested;

        public async Task<IReadOnlyDictionary<string, object>> ShowAsync(string commandId, string dialogJson, Func<IReadOnlyDictionary<string, JsonNode>, string> validate)
        {
            if (_active != null)
                return null;

            var dialog = JsonNode.Parse(dialogJson) as JsonObject;
            var request = new WordDialogRequest(TitleOf(dialog), FieldsOf(dialog), s_none, validate);
            _active = request;
            Requested?.Invoke(request);
            try
            {
                var values = await request.Completion.Task;
                return values?.ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.Ordinal);
            }
            finally
            {
                if (ReferenceEquals(_active, request))
                    _active = null;
            }
        }

        private static string TitleOf(JsonObject dialog) => dialog?["title"]?.GetValue<string>() ?? string.Empty;

        private static IReadOnlyList<WordDialogField> FieldsOf(JsonObject dialog)
        {
            var fields = new List<WordDialogField>();
            if (dialog?["fields"] is not JsonArray items)
                return fields;

            foreach (var item in items)
            {
                if (item is not JsonObject field)
                    continue;

                var type = TypeOf(field["type"]?.GetValue<string>());
                if (type == null)
                    continue;

                var name = field["name"]?.GetValue<string>() ?? string.Empty;
                var label = field["label"]?.GetValue<string>() ?? name;
                var defaults = DefaultOf(field["default"], type);
                var options = field["options"] as JsonArray;
                var values = new List<string>();
                var labels = new List<string>();
                if (options != null)
                {
                    foreach (var option in options)
                    {
                        var text = option?.GetValue<string>();
                        if (text == null)
                            continue;

                        var split = SplitOption(text);
                        values.Add(split.Value);
                        labels.Add(split.Label);
                    }
                }

                fields.Add(new WordDialogField(name, label, type, defaults, values, labels));
            }

            return fields;
        }

        private static string TypeOf(string type) => type switch
        {
            "none" => null,
            "bool" => "bool",
            "choice" => "choice",
            "color" => "color",
            "number" => "number",
            _ => "text",
        };

        private static string DefaultOf(JsonNode node, string type)
        {
            if (node == null)
                return null;

            if (type == "choice" && node is JsonValue text && text.TryGetValue<string>(out var value) && value.Length == 0)
                return null;

            var raw = node.ToJsonString();
            return raw.Contains('{') ? null : raw;
        }

        private static (string Value, string Label) SplitOption(string text)
        {
            var colon = text.IndexOf(':');
            if (colon <= 0 || text.Substring(0, colon).Contains(' '))
                return (text, text);

            return (text.Substring(0, colon), text.Substring(colon + 1));
        }
    }
}
