using DocumentFormat.OpenXml;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptPresentationOrder
    {
        public static void Insert(P.Presentation presentation, OpenXmlElement child) =>
            PptSchemaOrder.Insert(presentation, child, PptSchemaOrder.Presentation);

        public static void Replace(P.Presentation presentation, OpenXmlElement child) =>
            PptSchemaOrder.Replace(presentation, child, PptSchemaOrder.Presentation);

        public static int Remove(P.Presentation presentation, string localName) =>
            PptSchemaOrder.Remove(presentation, localName, PptSchemaOrder.Presentation);
    }
}
