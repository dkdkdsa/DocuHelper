using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneTableRow
    {
        public long HeightEmu { get; set; }

        public List<PptSceneTableCell> Cells { get; set; } = new();
    }
}
