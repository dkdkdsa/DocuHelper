using System.Collections.Generic;

namespace DocuHelperEditor.State
{
    public class SavedChatEntry
    {
        public ChatEntryKind Kind { get; set; }

        public string Text { get; set; }

        public string AskedQuestion { get; set; }

        public List<ContextChipModel> Chips { get; set; } = new();

        public List<SavedAttachment> Attachments { get; set; } = new();

        public List<string> Steps { get; set; } = new();

        public string Elapsed { get; set; }

        public AssistantResult Result { get; set; }
    }
}
