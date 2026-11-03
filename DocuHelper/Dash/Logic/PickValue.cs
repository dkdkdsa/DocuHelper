using System.Collections.Generic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class PickValue : DashValue
    {
        public string Table { get; set; }

        public int Row { get; set; } = 1;

        public string Column { get; set; }

        public override IEnumerable<string> Uses()
        {
            if (!string.IsNullOrEmpty(Table))
                yield return Table;
        }

        public override object Compute(DashRuntime runtime)
        {
            var table = runtime.Table(Table);
            var column = table.Require(Column);
            var index = Row > 0 ? Row - 1 : table.Rows.Count + Row;
            return index >= 0 && index < table.Rows.Count ? DashTable.Get(table.Rows[index], column) : null;
        }
    }
}
