using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptSource
    {
        public byte[] Stream { get; set; }

        public PptPersist Persist { get; set; }

        public PptRecord Document { get; set; }

        public PptPictureStore Pictures { get; set; }

        public PptTextStyles Styles { get; set; } = PptTextStyles.Defaults();

        public PptProperties Defaults { get; set; } = new();

        public List<string> Fonts { get; } = new();

        public List<PptRecord> TopLevel { get; } = new();
    }
}
