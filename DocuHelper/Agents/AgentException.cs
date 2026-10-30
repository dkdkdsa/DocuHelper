using System;

namespace DocuHelper.Agents
{
    public class AgentException : Exception
    {
        public AgentException(AgentFailure failure, string message, Exception innerException = null)
            : base(message, innerException)
        {
            Failure = failure;
        }

        public AgentFailure Failure { get; }
    }
}
