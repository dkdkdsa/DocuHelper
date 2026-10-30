using System.Collections.Generic;

namespace DocuHelperEditor.State
{
    public class ChatEntry
    {
        public ChatEntryKind Kind { get; set; }

        public string Text { get; set; }

        public string AskedQuestion { get; set; }

        public List<ContextChipModel> Chips { get; set; } = new();

        public List<AttachmentModel> Attachments { get; set; } = new();

        public TaskProgress Progress { get; set; }

        public AssistantResult Result { get; set; }

        public AssistantError Error { get; set; }
    }
}
