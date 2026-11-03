using System;
using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class ShareStep : DashStep
    {
        public string Column { get; set; }

        public string As { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
