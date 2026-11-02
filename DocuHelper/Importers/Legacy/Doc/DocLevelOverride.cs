namespace DocuHelper.Importers.Legacy.Doc
{
    internal sealed class DocLevelOverride
    {
        public int Level { get; set; }

        public int? StartAt { get; set; }

        public DocListLevel Format { get; set; }
    }
}
