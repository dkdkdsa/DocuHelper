using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptSlide
    {
        public List<PptShape> Shapes { get; } = new();

        public int Geometry { get; set; } = -1;

        public string Background { get; set; }

        public uint[] Scheme { get; set; }

        public List<string> Notes { get; } = new();
    }
}
