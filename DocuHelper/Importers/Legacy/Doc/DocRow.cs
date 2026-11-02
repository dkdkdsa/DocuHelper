using System.Collections.Generic;
using DocumentFormat.OpenXml;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocRow
    {
        public DocRow(DocTap properties, List<List<OpenXmlElement>> cells)
        {
            Properties = properties;
            Cells = cells;
        }

        public DocTap Properties { get; }

        public List<List<OpenXmlElement>> Cells { get; }
    }
}
