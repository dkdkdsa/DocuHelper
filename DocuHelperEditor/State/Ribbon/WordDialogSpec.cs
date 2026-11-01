using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed record WordDialogSpec(string Title, IReadOnlyList<WordDialogField> Fields, Func<IReadOnlyDictionary<string, JsonNode>, string> Validate = null);
}
