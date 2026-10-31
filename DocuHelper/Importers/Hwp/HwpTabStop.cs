namespace DocuHelper.Importers.Hwp
{
    public sealed class HwpTabStop
    {
        public int Position { get; set; }

        public string Type { get; set; } = "LEFT";

        public string Leader { get; set; } = "NONE";
    }
}
