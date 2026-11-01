namespace DocuHelper.Agents
{
    public class AgentUnverifiedKeyException : AgentException
    {
        public AgentUnverifiedKeyException(string message)
            : base(AgentFailure.Key, message)
        {
        }
    }
}
