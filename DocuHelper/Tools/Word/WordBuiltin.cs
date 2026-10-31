using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed record WordBuiltin(string Id, string Name, string Display, string Korean, StyleValues Type, int Priority, bool Quick);
}
