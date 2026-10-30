using System.Text.Json;

namespace DocuHelper.Agents
{
    public class AgentToolDefinition
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public JsonElement InputSchema { get; set; }
    }
}
