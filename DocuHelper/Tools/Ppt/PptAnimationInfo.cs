namespace DocuHelper.Tools.Ppt
{
    public class PptAnimationInfo
    {
        public int Order { get; set; }

        public uint ShapeId { get; set; }

        public string Effect { get; set; }

        public string Phase { get; set; } = "entrance";

        public string Start { get; set; } = "on_click";

        public int DurationMs { get; set; }

        public int DelayMs { get; set; }

        public string Option { get; set; }

        public string Sequence { get; set; } = "as_one";

        public uint? TriggerShapeId { get; set; }

        public bool IsOther { get; set; }

        public int? Paragraph { get; set; }
    }
}
