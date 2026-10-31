using System.Collections.Generic;

namespace DocuHelper.Tools.Excel
{
    internal sealed partial class FormulaEvaluator
    {
        private ExcelValue CallInfo(string name, List<FormulaNode> args)
        {
            switch (name)
            {
                case "ISNUMBER":
                    return ExcelValue.FromBoolean(Scalar(args[0]).IsNumber);
                case "ISTEXT":
                    return ExcelValue.FromBoolean(Scalar(args[0]).IsText);
                case "ISBLANK":
                    return ExcelValue.FromBoolean(Scalar(args[0]).IsBlank);
                case "ISERROR":
                    return ExcelValue.FromBoolean(Scalar(args[0]).IsError);
                case "ISERR":
                    var value = Scalar(args[0]);
                    return ExcelValue.FromBoolean(value.IsError && value.Text != ExcelValue.NotAvailable);
                case "ISNA":
                    return ExcelValue.FromBoolean(IsNotAvailable(Scalar(args[0])));
                case "IFNA":
                    value = Scalar(args[0]);
                    return IsNotAvailable(value) ? Scalar(args[1]) : value;
                case "NA":
                    return ExcelValue.FromError(ExcelValue.NotAvailable);
                default:
                    return null;
            }
        }

        private static bool IsNotAvailable(ExcelValue value) => value.IsError && value.Text == ExcelValue.NotAvailable;
    }
}
