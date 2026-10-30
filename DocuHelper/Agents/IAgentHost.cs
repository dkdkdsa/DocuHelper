using System.Threading;
using System.Threading.Tasks;

namespace DocuHelper.Agents
{
    public interface IAgentHost
    {
        void OnMessage(string text);

        void OnStepStarted(AgentStep step);

        void OnStepCompleted(AgentStep step);

        Task<string> AskUserAsync(AgentQuestion question, CancellationToken cancellationToken);

        Task WaitForRetryAsync(AgentRetry retry, CancellationToken cancellationToken);
    }
}
