using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SheetChartSeries
    {
        public string Name { get; set; }

        public string ColorHex { get; set; }

        public List<double?> Values { get; set; } = new();

        public List<double?> XValues { get; set; } = new();
    }
}
