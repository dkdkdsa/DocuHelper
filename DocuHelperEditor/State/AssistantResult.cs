using System.Collections.Generic;

namespace DocuHelperEditor.State
{
    public class AssistantResult
    {
        public List<ChangeGroup> Groups { get; set; } = new();

        public List<string> Untouched { get; set; } = new();

        public List<string> ReadOnlyFiles { get; set; } = new();

        public List<string> FollowUps { get; set; } = new();

        public bool IsSaved { get; set; }

        public string SavedAt { get; set; }
    }
}
