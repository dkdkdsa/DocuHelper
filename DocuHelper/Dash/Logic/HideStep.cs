using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Dash.Runtime;

namespace DocuHelper.Dash.Logic
{
    public sealed class HideStep : DashStep
    {
        public List<string> Columns { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();

        public override DashTable Apply(DashTable input, DashRuntime runtime)
        {
            var hidden = new HashSet<string>(Columns ?? new List<string>(), StringComparer.Ordinal);
            var kept = Enumerable.Range(0, input.Columns.Count).Where(at => !hidden.Contains(input.Columns[at])).ToList();
            if (kept.Count == input.Columns.Count)
                return input;

            var result = new DashTable(kept.Select(at => input.Columns[at]));
            foreach (var row in input.Rows)
                result.Rows.Add(kept.Select(at => DashTable.Get(row, at)).ToArray());

            return result;
        }
    }
}
