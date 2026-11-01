using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SlideChartSeries
    {
        public string Name { get; set; }

        public string Color { get; set; }

        public List<double?> Values { get; set; } = new();
    }
}
