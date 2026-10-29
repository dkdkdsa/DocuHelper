namespace DocuHelperEditor.State
{
    public class AssistantError
    {
        public AssistantErrorKind Kind { get; set; }

        public string Title { get; set; }

        public string Message { get; set; }

        public int Attempt { get; set; }

        public int RetryInSeconds { get; set; }
    }
}
