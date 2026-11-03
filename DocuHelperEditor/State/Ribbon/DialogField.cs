using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed record DialogField(string Name, string Label, string Type, string Default, IReadOnlyList<string> Options, IReadOnlyList<string> Labels);
}
