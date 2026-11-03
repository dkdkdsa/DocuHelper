using System.Collections.Generic;

namespace DocuHelper.Agents
{
    public class AgentDashMake
    {
        public string FileName { get; set; }

        public string Template { get; set; }

        public List<string> Sources { get; set; } = new();
    }
}
