using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Ppt
{
    public sealed class PptDiagramPlan
    {
        public PptDiagramPlan(IReadOnlyList<PptDiagramBox> boxes, IReadOnlyList<PptDiagramLink> links)
        {
            Boxes = boxes;
            Links = links;
        }

        public IReadOnlyList<PptDiagramBox> Boxes { get; }

        public IReadOnlyList<PptDiagramLink> Links { get; }

        public PptBox Bounds => PptBox.Bounds(Boxes.Select(b => b.Box));
    }
}
