using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneFill
    {
        public string Kind { get; set; } = "none";

        public string Color { get; set; }

        public int Alpha { get; set; } = 100;

        public string Color2 { get; set; }

        public double AngleDeg { get; set; }

        public List<PptSceneStop> Stops { get; set; } = new();

        public string ImageKey { get; set; }

        public DocumentFormat.OpenXml.Packaging.OpenXmlPart Owner { get; set; }
    }
}
