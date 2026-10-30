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
    }
}
