using System.Collections.Generic;

namespace DocuHelper.Agents
{
    public class AgentUsage
    {
        public string Model { get; set; }

        public long InputTokens { get; set; }

        public long CacheWriteTokens { get; set; }

        public long CacheWriteHourTokens { get; set; }

        public long CacheReadTokens { get; set; }

        public long OutputTokens { get; set; }

        public List<string> Tools { get; } = new();
    }
}
