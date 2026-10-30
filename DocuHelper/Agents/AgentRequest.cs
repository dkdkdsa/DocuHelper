using System.Collections.Generic;

namespace DocuHelper.Agents
{
    public class AgentRequest
    {
        public string ApiKey { get; set; }

        public string Prompt { get; set; }

        public string ActiveFile { get; set; }

        public string Selection { get; set; }

        public string Model { get; set; }

        public string Effort { get; set; }

        public bool AskBigChanges { get; set; } = true;

        public List<AgentAttachment> Attachments { get; set; } = new();
    }
}
