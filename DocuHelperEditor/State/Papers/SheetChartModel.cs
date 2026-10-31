using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SheetChartModel
    {
        public SheetChartType Type { get; set; }

        public SheetChartSubtype Subtype { get; set; }

        public string Title { get; set; }

        public string CategoryTitle { get; set; }

        public string ValueTitle { get; set; }

        public SheetChartLegend Legend { get; set; } = SheetChartLegend.Right;

        public bool DataLabels { get; set; }

        public List<string> Categories { get; set; } = new();

        public List<SheetChartSeries> Series { get; set; } = new();
    }
}
