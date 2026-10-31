namespace DocuHelper.Tools.Word
{
    public sealed class WordRichRun
    {
        public WordRichRun(string text, WordRunFormat format)
        {
            Text = WordText.Clean(text);
            Format = format;
        }

        public string Text { get; }

        public WordRunFormat Format { get; }
    }
}
