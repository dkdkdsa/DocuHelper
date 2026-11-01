using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    public class HwpSection
    {
        public HwpPage Page { get; set; }

        public List<HwpHeaderFooter> HeaderFooters { get; set; } = new();

        public HwpPageNumber PageNumber { get; set; }

        public bool HideFirstHeader { get; set; }

        public bool HideFirstFooter { get; set; }

        public int StartPage { get; set; }
    }
}
