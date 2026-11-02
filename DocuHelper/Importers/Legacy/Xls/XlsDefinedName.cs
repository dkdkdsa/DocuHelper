namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsDefinedName
    {
        public string Name { get; set; }

        public string Formula { get; set; }

        public int? LocalSheet { get; set; }

        public bool Hidden { get; set; }
    }
}
