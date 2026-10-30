using System.Threading;
using System.Threading.Tasks;

namespace DocuHelper.Agents
{
    public abstract class Agent
    {
        public abstract string Name { get; }

        public abstract Task<AgentResult> RunAsync(AgentRequest request, AgentToolbox toolbox, IAgentHost host, CancellationToken cancellationToken);

        public abstract void ResetConversation();

        public abstract Task VerifyKeyAsync(string apiKey, CancellationToken cancellationToken);
    }
}
