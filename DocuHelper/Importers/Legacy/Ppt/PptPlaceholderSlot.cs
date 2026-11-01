namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptPlaceholderSlot
    {
        public PptPlaceholderSlot(string xml, string name)
        {
            Xml = xml;
            Name = name;
        }

        public string Xml { get; }

        public string Name { get; }
    }
}
