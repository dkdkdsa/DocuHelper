using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed class DialogPresenter : IDialogPresenter
    {
        private static readonly IReadOnlyDictionary<string, JsonNode> s_none = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

        private DialogRequest _active;

        public event Action<DialogRequest> Requested;

        public bool CanOpen(string commandId, string dialogName) =>
            dialogName == null || NamedDialogs.TryGet(commandId, dialogName, out _);

        public Task<IReadOnlyDictionary<string, JsonNode>> AskFieldsAsync(string commandId, DialogSpec spec, IReadOnlyDictionary<string, JsonNode> defaults) =>
            OpenAsync(spec.Title, spec.Fields, defaults ?? s_none, spec.Validate);

        public async Task<JsonObject> AskArgsAsync(string commandId, string dialogName, JsonObject current)
        {
            if (!NamedDialogs.TryGet(commandId, dialogName, out var dialog))
                return null;

            var defaults = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
            foreach (var pair in DefaultsOf(dialog.Fields))
                defaults[pair.Key] = pair.Value;

            foreach (var field in dialog.Fields)
            {
                if (current?[field.Name] is JsonValue value && !(value.TryGetValue<string>(out var text) && text.Contains('{')))
                    defaults[field.Name] = value.DeepClone();
            }

            var values = await OpenAsync(dialog.Title, dialog.Fields, defaults, dialog.Validate);
            return values == null ? null : dialog.Build(values);
        }

        public void Cancel() => _active?.Completion.TrySetResult(null);

        public static IReadOnlyDictionary<string, JsonNode> DefaultsOf(IEnumerable<DialogField> fields)
        {
            var defaults = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                if (string.IsNullOrEmpty(field.Default))
                    continue;

                try
                {
                    defaults[field.Name] = JsonNode.Parse(field.Default);
                }
                catch (JsonException)
                {
                }
            }

            return defaults;
        }

        private Task<IReadOnlyDictionary<string, JsonNode>> OpenAsync(string title, IReadOnlyList<DialogField> fields, IReadOnlyDictionary<string, JsonNode> defaults, Func<IReadOnlyDictionary<string, JsonNode>, string> validate)
        {
            if (_active != null || Requested == null)
                return Task.FromResult<IReadOnlyDictionary<string, JsonNode>>(null);

            var request = new DialogRequest(title, fields, defaults, validate);
            _active = request;
            Requested?.Invoke(request);
            return Completed(request);
        }

        private async Task<IReadOnlyDictionary<string, JsonNode>> Completed(DialogRequest request)
        {
            try
            {
                return await request.Completion.Task;
            }
            finally
            {
                if (ReferenceEquals(_active, request))
                    _active = null;
            }
        }
    }
}
