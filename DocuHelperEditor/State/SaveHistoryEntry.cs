namespace DocuHelperEditor.State
{
    public class SaveHistoryEntry
    {
        public string Id { get; set; }

        public string FileId { get; set; }

        public string When { get; set; }

        public string What { get; set; }

        public bool IsCurrent { get; set; }

        public bool CanRestore => !IsCurrent;
    }
}
