using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneChart
    {
        public string Type { get; set; }

        public string Title { get; set; }

        public bool ShowTitle { get; set; }

        public double TitleSizePt { get; set; } = 14;

        public bool TitleBold { get; set; } = true;

        public List<string> Categories { get; set; } = new();

        public List<PptSceneSeries> Series { get; set; } = new();

        public string Legend { get; set; } = "none";

        public bool DataLabels { get; set; }

        public bool LabelValue { get; set; }

        public bool LabelPercent { get; set; }

        public bool LabelCategory { get; set; }

        public string LabelFormat { get; set; }

        public string LabelPosition { get; set; }

        public bool Stacked { get; set; }

        public bool PercentStacked { get; set; }

        public bool Horizontal { get; set; }

        public bool VaryColors { get; set; }

        public double GapWidth { get; set; } = 150;

        public double Overlap { get; set; }

        public double HoleSize { get; set; } = 50;

        public double FirstSliceAngle { get; set; }

        public double FontSizePt { get; set; } = 10;

        public string TextColor { get; set; }

        public bool ShowValueAxis { get; set; } = true;

        public bool ShowCategoryAxis { get; set; } = true;

        public double? AxisMin { get; set; }

        public double? AxisMax { get; set; }

        public double? MajorUnit { get; set; }

        public string ValueFormat { get; set; }

        public bool Gridlines { get; set; }

        public string GridColor { get; set; }

        public string ValueLineColor { get; set; }

        public string CategoryLineColor { get; set; }

        public bool ReverseCategories { get; set; }

        public bool MidCategory { get; set; }

        public double? CategoryLabelRotation { get; set; }

        public string PlotFill { get; set; }

        public string ChartFill { get; set; }
    }
}
