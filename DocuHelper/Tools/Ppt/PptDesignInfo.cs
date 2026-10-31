namespace DocuHelper.Tools.Ppt
{
    public class PptDesignInfo
    {
        public string ThemePresetId { get; set; }

        public string ThemeName { get; set; }

        public string ColorSchemeName { get; set; }

        public string FontSchemeName { get; set; }

        public string BackgroundStyle { get; set; } = "light";

        public long SlideWidthEmu { get; set; }

        public long SlideHeightEmu { get; set; }

        public string SlideSizeType { get; set; }
    }
}
