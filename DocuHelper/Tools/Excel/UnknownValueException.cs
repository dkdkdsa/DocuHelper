using System;

namespace DocuHelper.Tools.Excel
{
    internal sealed class UnknownValueException : Exception
    {
        public UnknownValueException()
            : base("This formula cannot be calculated here.")
        {
        }
    }
}
