using DocuHelper.Agents;

namespace DocuHelperEditor.State
{
    public class ModelOption
    {
        public string Id { get; set; }

        public AgentProvider Provider { get; set; } = AgentProvider.Claude;

        public string ApiId { get; set; }

        public string Name { get; set; }

        public string Cost { get; set; }

        public string Description { get; set; }

        public bool IsDefault { get; set; }
    }
}
