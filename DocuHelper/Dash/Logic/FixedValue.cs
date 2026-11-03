using System;
using System.Collections.Generic;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class FixedValue : DashValue
    {
        public double? Number { get; set; }

        public string Text { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();

        public override object Compute(DashRuntime runtime) => Number.HasValue ? Number.Value : Text;
    }
}
