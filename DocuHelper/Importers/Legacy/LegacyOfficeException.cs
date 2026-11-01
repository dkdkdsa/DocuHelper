using System;

namespace DocuHelper.Importers.Legacy
{
    public class LegacyOfficeException : Exception
    {
        public LegacyOfficeException(string message)
            : base(message)
        {
        }

        public LegacyOfficeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
