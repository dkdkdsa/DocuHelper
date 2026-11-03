namespace DocuHelper.Dash.Runtime
{
    public sealed class TextRun
    {
        public TextRun(TextRunKind kind, string text, string name = null)
        {
            Kind = kind;
            Text = text ?? string.Empty;
            Name = name;
        }

        public TextRunKind Kind { get; }

        public string Text { get; set; }

        public string Name { get; }
    }
}
