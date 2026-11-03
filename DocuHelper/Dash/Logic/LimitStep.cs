using System;
using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class LimitStep : DashStep
    {
        public int Count { get; set; }

        public string Rest { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
