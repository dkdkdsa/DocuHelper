using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptChartInfo
    {
        public string Title { get; set; }

        public string Type { get; set; }

        public List<string> Categories { get; set; } = new();

        public List<PptSeriesInfo> Series { get; set; } = new();
    }
}
