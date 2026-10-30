using System;
using System.Collections.Generic;
using DocuHelper.Docus;

namespace DocuHelperEditor.State
{
    public class SavedConversation
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string FileName { get; set; }

        public DocuType? FileType { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public List<SavedChatEntry> Entries { get; set; } = new();
    }
}
