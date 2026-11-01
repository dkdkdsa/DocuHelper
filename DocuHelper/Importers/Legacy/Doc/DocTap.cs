using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocTap
    {
        public const int DefaultGap = 108;

        public List<int> Centers { get; } = new();

        public List<DocTc> Cells { get; } = new();

        public DocBrc[] Borders { get; } = new DocBrc[6];

        public int RowHeight { get; set; }

        public bool Header { get; set; }

        public int Gap { get; set; } = DefaultGap;

        public int? Justification { get; set; }

        public DocTc Cell(int index)
        {
            while (Cells.Count <= index && Cells.Count < 64)
                Cells.Add(new DocTc());

            return index < Cells.Count ? Cells[index] : new DocTc();
        }
    }
}
