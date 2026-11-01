namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptRun
    {
        public string Text { get; set; }

        public bool IsBreak { get; set; }

        public string Field { get; set; }

        public PptCharProps Props { get; set; }
    }
}
