namespace DocuHelper.Tools.Word
{
    public sealed class WordObjectInfo
    {
        public int? Id { get; init; }

        public string Kind { get; init; }

        public string Name { get; init; }

        public string ParagraphId { get; init; }

        public int Page { get; init; }

        public double? WidthCm { get; init; }

        public double? HeightCm { get; init; }

        public string Wrap { get; init; }

        public string Alt { get; init; }

        public string Ref => Id == null ? null : "o:" + Id.Value;

        public string Anchor => Id == null ? null : WordAnchors.Object(Id.Value);
    }
}
