using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    public class HwpDocument
    {
        public List<HwpSection> Sections { get; } = new() { new HwpSection() };

        public HwpPage Page
        {
            get => Sections[0].Page;
            set => Sections[0].Page = value;
        }

        public List<HwpBlock> Blocks { get; set; } = new();

        public List<HwpNote> Notes { get; set; } = new();

        public List<HwpPicture> Pictures { get; set; } = new();

        public Dictionary<int, HwpNumberingDef> Numberings { get; set; } = new();

        public HashSet<HwpSkipped> Skipped { get; set; } = new();

        public List<HwpHeaderFooter> HeaderFooters => Sections[0].HeaderFooters;

        public HwpPageNumber PageNumber
        {
            get => Sections[0].PageNumber;
            set => Sections[0].PageNumber = value;
        }

        public bool HideFirstHeader
        {
            get => Sections[0].HideFirstHeader;
            set => Sections[0].HideFirstHeader = value;
        }

        public bool HideFirstFooter
        {
            get => Sections[0].HideFirstFooter;
            set => Sections[0].HideFirstFooter = value;
        }

        public int StartPage
        {
            get => Sections[0].StartPage;
            set => Sections[0].StartPage = value;
        }

        public bool Signed { get; set; }
    }
}
