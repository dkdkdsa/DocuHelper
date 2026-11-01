using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneSlide
    {
        public int Number { get; set; }

        public uint SlideId { get; set; }

        public string Anchor { get; set; }

        public bool IsHidden { get; set; }

        public string LayoutName { get; set; }

        public string LayoutType { get; set; }

        public bool HideMasterShapes { get; set; }

        public PptBackgroundInfo Background { get; set; }

        public List<PptSceneShape> Shapes { get; set; } = new();

        public List<PptSceneShape> Decor { get; set; } = new();

        public PptTransitionInfo Transition { get; set; }

        public List<PptAnimationInfo> Animations { get; set; } = new();

        public List<PptCommentInfo> Comments { get; set; } = new();

        public List<string> Notes { get; set; } = new();

        public PptFooterInfo FooterShown { get; set; }
    }
}
