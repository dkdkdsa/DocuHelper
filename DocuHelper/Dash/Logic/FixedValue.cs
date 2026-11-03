using System;
using System.Collections.Generic;

namespace DocuHelper.Dash.Logic
{
    public sealed class FixedValue : DashValue
    {
        public double? Number { get; set; }

        public string Text { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();
    }
}
