using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SheetChartModel
    {
        public SheetChartType Type { get; set; }

        public SheetChartSubtype Subtype { get; set; }

        public string Title { get; set; }

        public double TitleSize { get; set; } = 14;

        public bool TitleBold { get; set; }

        public bool TitleOverlay { get; set; }

        public string CategoryTitle { get; set; }

        public string ValueTitle { get; set; }

        public SheetChartLegend Legend { get; set; } = SheetChartLegend.Right;

        public bool DataLabels { get; set; }

        public bool LabelValue { get; set; }

        public bool LabelPercent { get; set; }

        public bool LabelCategory { get; set; }

        public bool LabelSeries { get; set; }

        public string LabelPosition { get; set; }

        public double FontSize { get; set; } = 10;

        public string TextColor { get; set; } = "595959";

        public string ChartFill { get; set; } = "FFFFFF";

        public string ChartBorder { get; set; }

        public bool Rounded { get; set; }

        public string PlotFill { get; set; }

        public bool ValueGridlines { get; set; } = true;

        public bool CategoryGridlines { get; set; }

        public string GridColor { get; set; } = "D9D9D9";

        public string AxisColor { get; set; } = "BFBFBF";

        public bool ShowCategoryAxis { get; set; } = true;

        public bool ShowValueAxis { get; set; } = true;

        public bool ValueAxisLine { get; set; }

        public bool CategoryAxisLine { get; set; } = true;

        public bool BetweenCategories { get; set; } = true;

        public bool TickMarks { get; set; }

        public string ValueFormat { get; set; }

        public double? Minimum { get; set; }

        public double? Maximum { get; set; }

        public double? MajorUnit { get; set; }

        public int GapWidth { get; set; } = 150;

        public int Overlap { get; set; }

        public bool VaryColors { get; set; }

        public double FirstSliceAngle { get; set; }

        public int HoleSize { get; set; } = 50;

        public List<string> Categories { get; set; } = new();

        public List<SheetChartSeries> Series { get; set; } = new();
    }
}
