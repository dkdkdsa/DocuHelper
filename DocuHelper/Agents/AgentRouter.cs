using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DocuHelper.Agents
{
    public class AgentRouter : Agent
    {
        private readonly Dictionary<AgentProvider, Agent> _agents;
        private readonly List<AgentTurn> _turns = new();
        private Agent _active;

        public AgentRouter(Agent claude, Agent chatGpt)
        {
            _agents = new Dictionary<AgentProvider, Agent>
            {
                [AgentProvider.Claude] = claude,
                [AgentProvider.OpenAi] = chatGpt,
            };
        }

        public override string Name => (_active ?? _agents[AgentProvider.Claude]).Name;

        public override async Task<AgentResult> RunAsync(AgentRequest request, AgentToolbox toolbox, IAgentHost host, CancellationToken cancellationToken)
        {
            var agent = _agents[request.Provider];
            if (agent != _active)
            {
                agent.RestoreConversation(_turns);
                _active = agent;
            }
            else if (request.TrimHistoryAbove > 0 && agent.LastPromptTokens > request.TrimHistoryAbove)
            {
                agent.RestoreConversation(_turns);
            }

            var result = await agent.RunAsync(request, toolbox, host, cancellationToken);

            var prompt = request.Prompt?.Trim();
            if (!string.IsNullOrWhiteSpace(prompt))
                _turns.Add(new AgentTurn { IsUser = true, Text = prompt });

            if (!string.IsNullOrWhiteSpace(result.Text))
                _turns.Add(new AgentTurn { IsUser = false, Text = result.Text.Trim() });

            return result;
        }

        public override void ResetConversation()
        {
            foreach (var agent in _agents.Values)
                agent.ResetConversation();

            _turns.Clear();
            _active = null;
        }

        public override void RestoreConversation(IReadOnlyList<AgentTurn> turns)
        {
            _turns.Clear();
            _turns.AddRange((turns ?? Array.Empty<AgentTurn>()).Where(turn => turn != null));
            _active?.RestoreConversation(_turns);
        }

        public override async Task VerifyKeyAsync(string apiKey, CancellationToken cancellationToken)
        {
            var provider = AgentKeys.Detect(apiKey);
            if (provider == null)
                throw new AgentException(AgentFailure.Key, "열쇠 모양이 맞지 않아요.");

            await VerifyKeyAsync(provider.Value, apiKey, cancellationToken);
        }

        public Task VerifyKeyAsync(AgentProvider provider, string apiKey, CancellationToken cancellationToken) =>
            _agents[provider].VerifyKeyAsync(apiKey, cancellationToken);
    }
}
