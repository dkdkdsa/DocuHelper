using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneSeries
    {
        public string Name { get; set; }

        public List<double?> Values { get; set; } = new();

        public string Color { get; set; }
    }
}
