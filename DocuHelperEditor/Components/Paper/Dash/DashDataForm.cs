using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal sealed record DashDataForm(
        string Title,
        IReadOnlyList<DialogField> Fields,
        IReadOnlyDictionary<string, JsonNode> Defaults,
        Func<IReadOnlyDictionary<string, JsonNode>, string> Validate,
        Func<IReadOnlyDictionary<string, JsonNode>, JsonObject> Build);
}
