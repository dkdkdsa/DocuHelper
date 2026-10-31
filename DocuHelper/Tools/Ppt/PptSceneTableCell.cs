using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneTableCell
    {
        public List<PptSceneParagraph> Paragraphs { get; set; } = new();

        public PptSceneFill Fill { get; set; }

        public PptSceneBorder BorderL { get; set; }

        public PptSceneBorder BorderT { get; set; }

        public PptSceneBorder BorderR { get; set; }

        public PptSceneBorder BorderB { get; set; }

        public int GridSpan { get; set; } = 1;

        public int RowSpan { get; set; } = 1;

        public bool HMerge { get; set; }

        public bool VMerge { get; set; }

        public string Anchor { get; set; } = "t";
    }
}
