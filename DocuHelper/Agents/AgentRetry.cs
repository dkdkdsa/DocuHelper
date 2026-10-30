using System;

namespace DocuHelper.Agents
{
    public class AgentRetry
    {
        public int Attempt { get; set; }

        public int MaxAttempts { get; set; }

        public TimeSpan Delay { get; set; }

        public AgentFailure Failure { get; set; }
    }
}
