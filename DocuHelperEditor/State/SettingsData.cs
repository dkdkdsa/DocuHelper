using System;

namespace DocuHelperEditor.State
{
    public class SettingsData
    {
        public string Root { get; set; }

        public string Model { get; set; }

        public string Effort { get; set; }

        public bool TrackChanges { get; set; }

        public bool AskBigChanges { get; set; } = true;

        public bool OpenLastFolder { get; set; }

        public bool HwpAlwaysHwpx { get; set; }

        public bool HwpNewAsHwp { get; set; }

        public string TextSize { get; set; }

        public string LastRunVersion { get; set; }

        public bool UpdateCheck { get; set; } = true;

        public bool UpdateAuto { get; set; } = true;

        public DateTime? ClaudeCheckedAt { get; set; }

        public DateTime? OpenAiCheckedAt { get; set; }

        public int? UsageCap { get; set; }

        public int? UsageAlert { get; set; }

        public string UsageAtCap { get; set; }

        public bool UsageShowCost { get; set; }

        public int? UsdRate { get; set; }

        public int? TrimAbove { get; set; }

        public bool AllowWeb { get; set; } = true;
    }
}
