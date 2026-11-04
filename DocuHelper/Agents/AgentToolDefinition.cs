using System.Text.Json;
using DocuHelper.Docus;

namespace DocuHelper.Agents
{
    public class AgentToolDefinition
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public JsonElement InputSchema { get; set; }

        public DocuType? Kind { get; set; }

        public bool IsWrite { get; set; }
    }
}
