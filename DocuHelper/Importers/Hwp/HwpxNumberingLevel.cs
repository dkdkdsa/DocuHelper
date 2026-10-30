namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpxNumberingLevel
    {
        public int Start { get; set; } = 1;

        public string Format { get; set; } = "DIGIT";

        public string Text { get; set; } = string.Empty;
    }
}
