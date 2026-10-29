using System.Collections.Generic;

namespace DocuHelper.Editors
{
    public record EditCommand(string Name, IReadOnlyDictionary<string, string> Arguments);
}
