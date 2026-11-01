using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    public class HwpNumberingDef
    {
        public bool Bullet { get; set; }

        public List<HwpNumberingLevelDef> Levels { get; set; } = new();
    }

    public class HwpNumberingLevelDef
    {
        public int Start { get; set; } = 1;

        public string Format { get; set; } = "DIGIT";

        public string Text { get; set; } = string.Empty;
    }
}
