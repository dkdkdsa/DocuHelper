using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptDocument
    {
        public int Width { get; set; } = 5760;

        public int Height { get; set; } = 4320;

        public List<PptSlide> Slides { get; } = new();

        public List<string> Fonts { get; } = new();
    }
}
