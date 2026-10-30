using DocuHelper.Docus;

namespace DocuHelper.Tools
{
    public class ToolResult
    {
        private ToolResult(bool isError, string content, DocuChange change, string detail)
        {
            IsError = isError;
            Content = content;
            Change = change;
            Detail = detail;
        }

        public bool IsError { get; }

        public string Content { get; }

        public DocuChange Change { get; }

        public string Detail { get; }

        public static ToolResult Ok(string content, string detail = null) => new(false, content, null, detail);

        public static ToolResult Changed(string content, DocuChange change, string detail = null) => new(false, content, change, detail);

        public static ToolResult Fail(string message) => new(true, message, null, null);
    }
}
