using System;
using System.Threading;
using System.Threading.Tasks;

namespace DocuHelper.Agents
{
    public class ChatGptAgent : Agent
    {
        public override string Name => "ChatGPT";

        public override Task<AgentResult> RunAsync(AgentRequest request, AgentToolbox toolbox, IAgentHost host, CancellationToken cancellationToken) =>
            throw new NotSupportedException("ChatGPT 도우미는 아직 준비 중이에요.");

        public override void ResetConversation()
        {
        }

        public override Task VerifyKeyAsync(string apiKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException("ChatGPT 도우미는 아직 준비 중이에요.");
    }
}
