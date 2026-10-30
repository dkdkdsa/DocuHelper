using System.Collections.Generic;

namespace DocuHelper.Agents
{
    public class AgentQuestion
    {
        public AgentQuestionKind Kind { get; set; }

        public string Question { get; set; }

        public string Hint { get; set; }

        public string FileName { get; set; }

        public List<AgentChoice> Choices { get; set; } = new();

        public List<AgentImpact> Impacts { get; set; } = new();

        public string UserValue { get; set; }

        public string SuggestedValue { get; set; }
    }
}
