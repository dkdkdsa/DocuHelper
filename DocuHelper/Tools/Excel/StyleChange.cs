namespace DocuHelper.Tools.Excel
{
    public sealed class StyleChange
    {
        public const string AutoColor = "auto";
        public const string NoFill = "none";

        public bool? Bold { get; set; }

        public bool? Italic { get; set; }

        public bool? Underline { get; set; }

        public string FontColor { get; set; }

        public string FillColor { get; set; }

        public string Horizontal { get; set; }

        public uint? NumberFormatId { get; set; }

        public bool HasFont => Bold.HasValue || Italic.HasValue || Underline.HasValue || FontColor != null;

        public bool IsEmpty => !HasFont && FillColor == null && Horizontal == null && !NumberFormatId.HasValue;

        public string Key => $"{Bold}|{Italic}|{Underline}|{FontColor}|{FillColor}|{Horizontal}|{NumberFormatId}";
    }
}
