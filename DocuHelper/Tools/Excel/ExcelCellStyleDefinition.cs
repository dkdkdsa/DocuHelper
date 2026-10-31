namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelCellStyleDefinition
    {
        public ExcelCellStyleDefinition(string name, string koreanName, uint builtinId)
        {
            Name = name;
            KoreanName = koreanName;
            BuiltinId = builtinId;
        }

        public string Name { get; }

        public string KoreanName { get; }

        public uint BuiltinId { get; }

        public ExcelStyleColor FontColor { get; init; }

        public bool Bold { get; init; }

        public bool Italic { get; init; }

        public bool Underline { get; init; }

        public double? FontSize { get; init; }

        public bool MajorFont { get; init; }

        public ExcelStyleColor Fill { get; init; }

        public ExcelStyleBorder Border { get; init; }

        public uint? NumberFormatId { get; init; }

        public bool IsNormal => BuiltinId == 0;

        public bool DefinesFont => FontColor != null || Bold || Italic || Underline || FontSize.HasValue || MajorFont;

        public bool DefinesFill => Fill != null;

        public bool DefinesBorder => Border != null;

        public bool DefinesNumberFormat => NumberFormatId.HasValue;
    }
}
