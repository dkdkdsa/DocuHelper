using System.Collections.Generic;

namespace DocuHelper.Tools.Excel
{
    public sealed class PagePlan
    {
        public PagePlan(PageSetupModel setup, double paperWidthCm, double paperHeightCm, double contentWidthCm, double contentHeightCm, double scale, IReadOnlyList<PlannedPage> pages)
        {
            Setup = setup;
            PaperWidthCm = paperWidthCm;
            PaperHeightCm = paperHeightCm;
            ContentWidthCm = contentWidthCm;
            ContentHeightCm = contentHeightCm;
            Scale = scale;
            Pages = pages;
        }

        public PageSetupModel Setup { get; }

        public double PaperWidthCm { get; }

        public double PaperHeightCm { get; }

        public double ContentWidthCm { get; }

        public double ContentHeightCm { get; }

        public double Scale { get; }

        public IReadOnlyList<PlannedPage> Pages { get; }
    }
}
