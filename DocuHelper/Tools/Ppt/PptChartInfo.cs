using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptChartInfo
    {
        public string Title { get; set; }

        public string Type { get; set; }

        public bool HasTitle { get; set; }

        public bool Stacked { get; set; }

        public string Grouping { get; set; }

        public string Legend { get; set; } = "none";

        public bool DataLabels { get; set; }

        public int GroupCount { get; set; }

        public List<string> Categories { get; set; } = new();

        public List<PptSeriesInfo> Series { get; set; } = new();
    }
}
