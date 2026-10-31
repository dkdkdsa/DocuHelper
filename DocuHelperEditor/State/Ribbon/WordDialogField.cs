using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed record WordDialogField(string Name, string Label, string Type, string Default, IReadOnlyList<string> Options, IReadOnlyList<string> Labels);
}
