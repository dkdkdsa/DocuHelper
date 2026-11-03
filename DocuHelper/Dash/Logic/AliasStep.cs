using System;
using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class AliasStep : DashStep
    {
        public string Column { get; set; }

        public Dictionary<string, string> Map { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
