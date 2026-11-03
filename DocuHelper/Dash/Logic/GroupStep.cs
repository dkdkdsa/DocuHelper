using System;
using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class GroupStep : DashStep
    {
        public List<string> By { get; set; }

        public List<string> Sum { get; set; }

        public string Count { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
