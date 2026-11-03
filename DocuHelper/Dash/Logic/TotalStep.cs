using System;
using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class TotalStep : DashStep
    {
        public string Label { get; set; }

        public string Op { get; set; }

        public List<string> Columns { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
