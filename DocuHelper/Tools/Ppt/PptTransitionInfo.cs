namespace DocuHelper.Tools.Ppt
{
    public class PptTransitionInfo
    {
        public string Effect { get; set; } = "none";

        public string Option { get; set; }

        public int DurationMs { get; set; }

        public bool AdvanceOnClick { get; set; } = true;

        public int? AdvanceAfterMs { get; set; }

        public string SoundName { get; set; }

        public string Raw { get; set; }
    }
}
