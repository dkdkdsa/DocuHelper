using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    internal sealed partial class FormulaEvaluator
    {
        private ExcelValue Subtotal(List<FormulaNode> args)
        {
            var problem = NumberArgument(args[0], out var number);
            if (problem != null)
                return problem;

            var code = (int)Math.Clamp(Math.Truncate(number), 0, 1000);
            var skipHidden = code > 100;
            var function = skipHidden ? code - 100 : code;
            if (function < 1 || function > 11)
                return ExcelValue.FromError(ExcelValue.BadValue);

            var numbers = new List<double>();
            long filled = 0;
            for (var i = 1; i < args.Count; i++)
            {
                if (args[i].Kind == FormulaNodeKind.Missing)
                    throw new UnknownValueException();

                var operand = Eval(args[i]);
                if (!operand.IsRange)
                {
                    if (operand.Value.IsError)
                        return operand.Value;

                    throw new UnknownValueException();
                }

                var error = CollectVisible(operand, skipHidden, function, numbers, ref filled);
                if (error != null)
                    return error;
            }

            return SubtotalResult(function, numbers, filled);
        }

        private ExcelValue CollectVisible(FormulaOperand operand, bool skipHidden, int function, List<double> numbers, ref long filled)
        {
            var sheet = operand.Sheet;
            var clipped = Clip(sheet, operand.Range);
            if (clipped == null)
                return null;

            var filtered = sheet.FilteredRows();
            for (var row = clipped.Top; row <= clipped.Bottom; row++)
            {
                if (sheet.IsRowHidden(row) && (skipHidden || filtered.Any(span => row >= span.Top && row <= span.Bottom)))
                    continue;

                for (var column = clipped.Left; column <= clipped.Right; column++)
                {
                    var cell = sheet.FindCell(column, row);
                    if (cell == null || sheet.ContainsSubtotal(cell))
                        continue;

                    var value = _calculator.GetValue(sheet, column, row);
                    if (function == 3)
                    {
                        if (!value.IsBlank)
                            filled++;
                    }
                    else if (value.IsError)
                    {
                        if (function != 2)
                            return value;
                    }
                    else if (value.IsNumber)
                    {
                        numbers.Add(value.Number);
                    }
                }
            }

            return null;
        }

        private static ExcelValue SubtotalResult(int function, List<double> numbers, long filled)
        {
            var count = numbers.Count;
            switch (function)
            {
                case 1:
                    return count == 0 ? ExcelValue.FromError(ExcelValue.DivideByZero) : ExcelValue.FromNumber(numbers.Sum() / count);
                case 2:
                    return ExcelValue.FromNumber(count);
                case 3:
                    return ExcelValue.FromNumber(filled);
                case 4:
                    return ExcelValue.FromNumber(count == 0 ? 0 : numbers.Max());
                case 5:
                    return ExcelValue.FromNumber(count == 0 ? 0 : numbers.Min());
                case 6:
                    return ExcelValue.FromNumber(count == 0 ? 0 : numbers.Aggregate(1.0, (a, b) => a * b));
                case 7:
                    return Deviation(numbers, true, true);
                case 8:
                    return Deviation(numbers, false, true);
                case 9:
                    return ExcelValue.FromNumber(numbers.Sum());
                case 10:
                    return Deviation(numbers, true, false);
                default:
                    return Deviation(numbers, false, false);
            }
        }

        private static ExcelValue Deviation(List<double> numbers, bool sample, bool root)
        {
            var count = numbers.Count;
            if (count < (sample ? 2 : 1))
                return ExcelValue.FromError(ExcelValue.DivideByZero);

            var mean = numbers.Sum() / count;
            var variance = numbers.Sum(n => (n - mean) * (n - mean)) / (sample ? count - 1 : count);
            return ExcelValue.FromNumber(root ? Math.Sqrt(variance) : variance);
        }
    }
}
