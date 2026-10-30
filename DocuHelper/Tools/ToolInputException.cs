using System;

namespace DocuHelper.Tools
{
    public class ToolInputException : Exception
    {
        public ToolInputException(string message)
            : base(message)
        {
        }
    }
}
