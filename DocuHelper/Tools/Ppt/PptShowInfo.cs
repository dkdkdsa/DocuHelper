namespace DocuHelper.Tools.Ppt
{
    public class PptShowInfo
    {
        public string Type { get; set; } = "presenter";

        public bool Loop { get; set; }

        public bool UseTimings { get; set; } = true;

        public bool ShowAnimation { get; set; } = true;

        public bool ShowNarration { get; set; } = true;

        public bool MediaControls { get; set; } = true;

        public PptShowRange Range { get; set; }
    }
}
