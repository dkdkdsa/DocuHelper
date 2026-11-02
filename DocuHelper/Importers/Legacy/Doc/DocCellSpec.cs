using System.Collections.Generic;
using DocumentFormat.OpenXml;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocCellSpec
    {
        public int Left { get; set; }

        public int Right { get; set; }

        public int GridStart { get; set; }

        public int GridEnd { get; set; }

        public DocTc Properties { get; set; }

        public List<OpenXmlElement> Content { get; set; }
    }
}
