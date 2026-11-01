namespace DocuHelper.Importers.Legacy.Xls
{
    internal sealed class XlsCell
    {
        public int Row { get; set; }

        public int Column { get; set; }

        public int Style { get; set; }

        public XlsCellKind Kind { get; set; }

        public double Number { get; set; }

        public string Text { get; set; }

        public byte Code { get; set; }

        public string Formula { get; set; }

        public string ArrayRange { get; set; }

        public byte[] Tokens { get; set; }

        public byte[] Extra { get; set; }
    }
}
