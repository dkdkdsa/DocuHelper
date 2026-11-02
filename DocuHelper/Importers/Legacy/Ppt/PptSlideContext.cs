using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptSlideContext
    {
        public PptPictureStore Pictures { get; set; }

        public PptProperties Defaults { get; set; } = new();

        public uint[] Scheme { get; set; }

        public PptTextStyles Styles { get; set; } = PptTextStyles.Defaults();

        public List<PptOutlineText> Outline { get; set; } = new();

        public Dictionary<int, PptRect> MasterAnchors { get; set; } = new();
    }
}
