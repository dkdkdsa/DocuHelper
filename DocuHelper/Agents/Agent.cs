using System.Threading;
using System.Threading.Tasks;

namespace DocuHelper.Agents
{
    public abstract class Agent
    {
        public abstract string Name { get; }

        public abstract Task<string> RunAsync(Workspace workspace, string prompt, CancellationToken cancellationToken = default);
    }
}
