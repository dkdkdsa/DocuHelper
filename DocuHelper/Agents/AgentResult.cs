using System.Collections.Generic;

namespace DocuHelper.Agents
{
    public class AgentResult
    {
        public string Text { get; set; }

        public List<AgentChange> Changes { get; set; } = new();

        public List<string> ReadOnlyFiles { get; set; } = new();

        public List<string> Untouched { get; set; } = new();

        public List<string> FollowUps { get; set; } = new();
    }
}
