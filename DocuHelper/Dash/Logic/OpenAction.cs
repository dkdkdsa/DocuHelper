using System;
using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class OpenAction : DashAction
    {
        public string File { get; set; }

        public string Sheet { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
