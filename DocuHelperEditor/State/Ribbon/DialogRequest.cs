using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed class DialogRequest
    {
        public DialogRequest(string title, IReadOnlyList<DialogField> fields, IReadOnlyDictionary<string, JsonNode> defaults, Func<IReadOnlyDictionary<string, JsonNode>, string> validate)
        {
            Title = title;
            Fields = fields;
            Defaults = defaults;
            Validate = validate;
        }

        public string Title { get; }

        public IReadOnlyList<DialogField> Fields { get; }

        public IReadOnlyDictionary<string, JsonNode> Defaults { get; }

        public Func<IReadOnlyDictionary<string, JsonNode>, string> Validate { get; }

        public TaskCompletionSource<IReadOnlyDictionary<string, JsonNode>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
