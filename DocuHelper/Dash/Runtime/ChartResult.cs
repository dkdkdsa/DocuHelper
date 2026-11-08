using System.Collections.Generic;

namespace DocuHelper.Dash.Runtime
{
    public sealed class ChartResult : ElementResult
    {
        public string ChartKind { get; set; }

        public List<string> Categories { get; set; } = new();

        public List<ChartSeriesResult> Series { get; set; } = new();

        public int Emphasis { get; set; } = -1;

        public List<bool> Dimmed { get; set; } = new();

        public bool Labels { get; set; }

        public List<double?> Positions { get; set; } = new();

        public RangeSliderResult Control { get; set; }

        public List<IReadOnlyDictionary<string, object>> RowItems { get; } = new();
    }
}
