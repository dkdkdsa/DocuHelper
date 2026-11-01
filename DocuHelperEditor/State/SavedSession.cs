using System.Collections.Generic;

namespace DocuHelperEditor.State
{
    public class SavedSession
    {
        public List<string> OpenFiles { get; set; } = new();

        public string ActiveFile { get; set; }

        public string ConversationId { get; set; }
    }
}
