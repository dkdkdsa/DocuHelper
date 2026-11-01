using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptMasterInfo
    {
        public PptTextStyles Styles { get; set; } = PptTextStyles.Defaults();

        public uint[] Scheme { get; set; }

        public Dictionary<int, PptRect> Anchors { get; } = new();

        public string Background { get; set; }
    }
}
