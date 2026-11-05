using System.Collections.Generic;

namespace DocuHelper.Dash.Runtime
{
    public sealed record QuotedColumns(string Expression, IReadOnlyDictionary<string, int> Columns);
}
