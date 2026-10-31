using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptScene
    {
        public long SlideWidthEmu { get; set; }

        public long SlideHeightEmu { get; set; }

        public PptThemeInfo Theme { get; set; }

        public List<PptLayoutInfo> Layouts { get; set; } = new();

        public PptShowInfo Show { get; set; }

        public List<PptCustomShowInfo> CustomShows { get; set; } = new();

        public List<PptSectionInfo> Sections { get; set; } = new();

        public List<PptSceneSlide> Slides { get; set; } = new();
    }
}
