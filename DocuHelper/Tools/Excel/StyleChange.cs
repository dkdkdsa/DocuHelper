namespace DocuHelper.Tools.Excel
{
    public sealed class StyleChange
    {
        public bool? Bold { get; set; }

        public bool? Italic { get; set; }

        public bool? Underline { get; set; }

        public string Horizontal { get; set; }

        public uint? NumberFormatId { get; set; }

        public bool HasFont => Bold.HasValue || Italic.HasValue || Underline.HasValue;

        public bool IsEmpty => !HasFont && Horizontal == null && !NumberFormatId.HasValue;

        public string Key => $"{Bold}|{Italic}|{Underline}|{Horizontal}|{NumberFormatId}";
    }
}
