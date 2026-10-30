using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSeriesInfo
    {
        public string Name { get; set; }

        public List<double?> Values { get; set; } = new();
    }
}
