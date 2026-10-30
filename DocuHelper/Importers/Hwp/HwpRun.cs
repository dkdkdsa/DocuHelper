namespace DocuHelper.Importers.Hwp
{
    public class HwpRun
    {
        public string Text { get; set; }

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public bool Strike { get; set; }

        public bool Superscript { get; set; }

        public bool Subscript { get; set; }

        public int Size { get; set; }

        public string Color { get; set; }

        public string Font { get; set; }
    }
}
