using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    internal sealed record WordCommandSpec(
        string Id,
        string Label,
        RibbonCommandClass Class,
        string Action,
        string Tool,
        string Args,
        string Prompt,
        string Reason,
        IReadOnlyList<WordMenuSpec> Menu,
        WordDialogSpec Dialog);
}
