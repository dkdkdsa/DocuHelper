using System;

namespace DocuHelper
{
    public class HistoryEntry
    {
        public string Id { get; set; }

        public string FileName { get; set; }

        public DateTime SavedAt { get; set; }

        public string What { get; set; }

        public string Path { get; set; }
    }
}
