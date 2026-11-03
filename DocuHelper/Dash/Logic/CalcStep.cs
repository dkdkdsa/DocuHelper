using System;
using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class CalcStep : DashStep
    {
        public string As { get; set; }

        public string Expression { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
