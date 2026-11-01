using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SheetChartSeries
    {
        public string Name { get; set; }

        public string ColorHex { get; set; }

        public string LineHex { get; set; }

        public double LineWidth { get; set; } = 2.25;

        public bool Smooth { get; set; }

        public string Marker { get; set; }

        public double MarkerSize { get; set; } = 7;

        public string Format { get; set; }

        public List<string> PointColors { get; set; } = new();

        public List<double?> Values { get; set; } = new();

        public List<double?> XValues { get; set; } = new();
    }
}
