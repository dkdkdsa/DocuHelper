using System.Collections.Generic;
using DocumentFormat.OpenXml;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocTableState
    {
        public List<DocRow> Rows { get; } = new();

        public List<List<OpenXmlElement>> Pending { get; private set; } = new();

        public List<OpenXmlElement> Current { get; private set; } = new();

        public void EndCell()
        {
            Pending.Add(Current);
            Current = new List<OpenXmlElement>();
        }

        public void EndRow(DocTap properties)
        {
            if (Current.Count > 0)
                EndCell();

            if (Pending.Count > 0)
                Rows.Add(new DocRow(properties, Pending));

            Pending = new List<List<OpenXmlElement>>();
        }
    }
}
