using System.Collections.Generic;

namespace DocuHelper.Dash.Runtime
{
    internal sealed class DashCalculation
    {
        public DashCalculation(IReadOnlyDictionary<string, object> results, IReadOnlyList<string> problems)
        {
            Results = results;
            Problems = problems;
        }

        public IReadOnlyDictionary<string, object> Results { get; }

        public IReadOnlyList<string> Problems { get; }
    }
}
