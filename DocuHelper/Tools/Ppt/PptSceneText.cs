using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneText
    {
        public List<PptSceneParagraph> Paragraphs { get; set; } = new();

        public string Anchor { get; set; } = "t";

        public long InsetL { get; set; } = 91440;

        public long InsetT { get; set; } = 45720;

        public long InsetR { get; set; } = 91440;

        public long InsetB { get; set; } = 45720;

        public bool Wrap { get; set; } = true;

        public PptSceneAutofit Autofit { get; set; }

        public string Vertical { get; set; }

        public int Columns { get; set; } = 1;
    }
}
