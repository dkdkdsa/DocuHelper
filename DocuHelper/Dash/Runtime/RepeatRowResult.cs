using System.Collections.Generic;
using DocuHelper.Dash.Logic;

namespace DocuHelper.Dash.Runtime
{
    public sealed class RepeatRowResult : ElementResult
    {
        public int Row { get; set; }

        public DashAction OnClick { get; set; }

        public Dictionary<string, object> Item { get; set; } = new();
    }
}
