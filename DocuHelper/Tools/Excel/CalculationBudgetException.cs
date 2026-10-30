using System;

namespace DocuHelper.Tools.Excel
{
    internal sealed class CalculationBudgetException : Exception
    {
        public CalculationBudgetException()
            : base("The workbook is too large to recalculate here.")
        {
        }
    }
}
