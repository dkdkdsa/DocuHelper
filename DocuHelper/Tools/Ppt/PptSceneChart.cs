using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneChart
    {
        public string Type { get; set; }

        public string Title { get; set; }

        public List<string> Categories { get; set; } = new();

        public List<PptSceneSeries> Series { get; set; } = new();

        public string Legend { get; set; } = "none";

        public bool DataLabels { get; set; }

        public bool Stacked { get; set; }

        public bool Horizontal { get; set; }
    }
}
