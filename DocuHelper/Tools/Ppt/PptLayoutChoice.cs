using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Tools.Ppt
{
    public class PptLayoutChoice
    {
        public PptLayoutChoice(SlideLayoutPart layout, string kind, SlideMasterPart master)
        {
            Layout = layout;
            Kind = kind;
            Master = master;
        }

        public SlideLayoutPart Layout { get; private set; }

        public string Kind { get; }

        public SlideMasterPart Master { get; }

        public bool IsNew => Layout == null;

        public bool Created { get; private set; }

        public void Attach(SlideLayoutPart layout)
        {
            Layout = layout;
            Created = true;
        }
    }
}
