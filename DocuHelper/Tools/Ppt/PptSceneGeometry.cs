using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneGeometry
    {
        public string Preset { get; set; }

        public List<double> Adjust { get; set; } = new();

        public PptSceneCustomPath CustomPath { get; set; }
    }
}
