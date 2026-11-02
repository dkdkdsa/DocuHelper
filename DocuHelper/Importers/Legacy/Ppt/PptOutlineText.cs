namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptOutlineText
    {
        public int TextType { get; set; }

        public string Text { get; set; } = string.Empty;

        public PptRecord Style { get; set; }
    }
}
