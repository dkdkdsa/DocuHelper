namespace DocuHelper.Tools.Excel
{
    public sealed class StyleChange
    {
        public const string AutoColor = "auto";
        public const string NoFill = "none";
        public const string NoBorder = "none";

        public bool? Bold { get; set; }

        public bool? Italic { get; set; }

        public bool? Underline { get; set; }

        public string FontColor { get; set; }

        public string FontName { get; set; }

        public double? FontSize { get; set; }

        public string FillColor { get; set; }

        public string Horizontal { get; set; }

        public string Vertical { get; set; }

        public bool? Wrap { get; set; }

        public uint? NumberFormatId { get; set; }

        public bool? Locked { get; set; }

        public string StyleName { get; set; }

        public ExcelBorder Top { get; set; }

        public ExcelBorder Bottom { get; set; }

        public ExcelBorder Left { get; set; }

        public ExcelBorder Right { get; set; }

        public bool HasFont => Bold.HasValue || Italic.HasValue || Underline.HasValue || FontColor != null || FontName != null || FontSize.HasValue;

        public bool HasAlignment => Horizontal != null || Vertical != null || Wrap.HasValue;

        public bool HasBorder => Top != null || Bottom != null || Left != null || Right != null;

        public bool IsEmpty => !HasFont && !HasAlignment && !HasBorder && FillColor == null && !NumberFormatId.HasValue && !Locked.HasValue && StyleName == null;

        public string Key => $"{Bold}|{Italic}|{Underline}|{FontColor}|{FontName}|{FontSize}|{FillColor}|{Horizontal}|{Vertical}|{Wrap}|{NumberFormatId}|{Top}|{Bottom}|{Left}|{Right}|{Locked}|{StyleName}";

        public StyleChange Clone() => (StyleChange)MemberwiseClone();
    }
}
