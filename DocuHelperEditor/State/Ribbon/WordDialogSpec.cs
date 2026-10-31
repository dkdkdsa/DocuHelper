using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    public sealed record WordDialogSpec(string Title, IReadOnlyList<WordDialogField> Fields);
}
