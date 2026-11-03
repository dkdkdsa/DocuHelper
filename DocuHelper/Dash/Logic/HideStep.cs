using System;
using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class HideStep : DashStep
    {
        public List<string> Columns { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
