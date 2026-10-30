using System;

namespace DocuHelper.Agents
{
    public class AgentStep
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");

        public string Label { get; set; }

        public string Detail { get; set; }

        public string FileName { get; set; }
    }
}
