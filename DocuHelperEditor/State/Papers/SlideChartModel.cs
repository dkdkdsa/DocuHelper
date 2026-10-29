using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public class SlideChartModel
    {
        public string Label { get; set; }

        public SlideChartKind Kind { get; set; }

        public List<SlideChartBar> Bars { get; set; } = new();

        public bool HasBars => Bars != null && Bars.Count > 0;

        public double MaxValue => HasBars ? Bars.Max(b => b.Value) : 0;

        public double RatioOf(SlideChartBar bar)
        {
            var max = MaxValue;
            if (bar == null || max <= 0)
                return 0;

            return Math.Clamp(bar.Value / max, 0, 1);
        }

        public bool IsTop(SlideChartBar bar) => bar != null && HasBars && bar.Value >= MaxValue;
    }
}
