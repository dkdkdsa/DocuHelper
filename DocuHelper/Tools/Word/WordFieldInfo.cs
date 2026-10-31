namespace DocuHelper.Tools.Word
{
    public sealed class WordFieldInfo
    {
        public string Name { get; init; }

        public string Instruction { get; init; }

        public string Result { get; init; } = string.Empty;

        public string ParagraphId { get; init; }

        public int Page { get; init; }

        public int Nested { get; set; }
    }
}
