using System;

namespace DocuHelper.Importers.Hwp
{
    public class HwpException : Exception
    {
        public HwpException(string message)
            : base(message)
        {
        }

        public HwpException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
