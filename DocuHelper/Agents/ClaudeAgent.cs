using System;
using System.Threading;
using System.Threading.Tasks;

namespace DocuHelper.Agents
{
    public class ClaudeAgent : Agent
    {
        public override string Name => "Claude";

        public override Task<string> RunAsync(Workspace workspace, string prompt, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
