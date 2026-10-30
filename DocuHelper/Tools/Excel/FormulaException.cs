using System;

namespace DocuHelper.Tools.Excel
{
    public class FormulaException : Exception
    {
        public FormulaException(string message)
            : base(message)
        {
        }
    }
}
