using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneTable
    {
        public List<long> Columns { get; set; } = new();

        public List<PptSceneTableRow> Rows { get; set; } = new();

        public string StyleKey { get; set; }

        public bool FirstRow { get; set; }

        public bool BandRow { get; set; }

        public bool FirstCol { get; set; }

        public bool LastRow { get; set; }

        public bool LastCol { get; set; }
    }
}
