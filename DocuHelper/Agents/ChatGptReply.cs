using System.Net;

namespace DocuHelper.Agents
{
    public class ChatGptReply
    {
        public HttpStatusCode Status { get; set; }

        public string Body { get; set; }

        public bool IsSuccess => (int)Status is >= 200 and < 300;
    }
}
