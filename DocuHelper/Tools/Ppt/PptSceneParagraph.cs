using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneParagraph
    {
        public int Level { get; set; }

        public string Align { get; set; } = "l";

        public PptSceneBullet Bullet { get; set; }

        public long MarginLeftEmu { get; set; }

        public long IndentEmu { get; set; }

        public PptSceneSpacing LineSpacing { get; set; }

        public double SpaceBeforePt { get; set; }

        public double SpaceAfterPt { get; set; }

        public double SpaceBeforePct { get; set; }

        public double SpaceAfterPct { get; set; }

        public List<PptSceneRun> Runs { get; set; } = new();
    }
}
