using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneSeries
    {
        public string Name { get; set; }

        public List<double?> Values { get; set; } = new();

        public string Color { get; set; }

        public string Type { get; set; }

        public Dictionary<int, string> PointColors { get; set; } = new();

        public double? LineWidthPt { get; set; }

        public string Marker { get; set; }

        public double MarkerSizePt { get; set; } = 5;

        public string MarkerColor { get; set; }

        public bool Smooth { get; set; }

        public string Format { get; set; }
    }
}
