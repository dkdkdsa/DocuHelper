using System.Collections.Generic;

namespace DocuHelper.Dash.Runtime
{
    public sealed class ChartSeriesResult
    {
        public string Name { get; set; }

        public List<double?> Values { get; set; } = new();

        public List<string> Labels { get; set; } = new();
    }
}
