using System;

namespace DocuHelperEditor.Services
{
    public class ClipboardException : Exception
    {
        public ClipboardException(string message)
            : base(message)
        {
        }

        public ClipboardException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
