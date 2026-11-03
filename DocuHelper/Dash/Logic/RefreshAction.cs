using System;
using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class RefreshAction : DashAction
    {
        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
