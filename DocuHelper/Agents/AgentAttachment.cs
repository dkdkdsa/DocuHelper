namespace DocuHelper.Agents
{
    public class AgentAttachment
    {
        public string Name { get; set; }

        public AgentAttachmentKind Kind { get; set; }

        public string MediaType { get; set; }

        public byte[] Data { get; set; }

        public string Text { get; set; }
    }
}
