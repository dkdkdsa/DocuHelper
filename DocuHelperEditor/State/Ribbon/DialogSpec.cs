using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed record DialogSpec(
        string Title,
        IReadOnlyList<DialogField> Fields,
        Func<IReadOnlyDictionary<string, JsonNode>, string> Validate = null,
        Func<IReadOnlyDictionary<string, JsonNode>, JsonObject> Build = null,
        string Tool = null);
}
