using System.Collections.Generic;

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
    }
}
