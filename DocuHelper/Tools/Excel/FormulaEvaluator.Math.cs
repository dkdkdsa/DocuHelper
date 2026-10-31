using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    internal sealed partial class FormulaEvaluator
    {
        private const long FullColumnRows = 65536;
        private const int FullRowColumns = 16384;

        private ExcelValue CallMath(string name, List<FormulaNode> args)
        {
            switch (name)
            {
                case "INT":
                    return NumberArgument(args[0], out var number) ?? ExcelValue.FromNumber(Math.Floor(number));
                case "MOD":
                    return Mod(args);
                case "TRUNC":
                    return Truncate(args);
                case "POWER":
                    return BinaryValues("^", Scalar(args[0]), Scalar(args[1]));
                case "SQRT":
                    var problem = NumberArgument(args[0], out number);
                    if (problem != null)
                        return problem;

                    return number < 0 ? ExcelValue.FromError(ExcelValue.BadNumber) : ExcelValue.FromNumber(Math.Sqrt(number));
                case "PRODUCT":
                    return Aggregate(args, numbers => numbers.Count == 0 ? 0 : numbers.Aggregate(1.0, (a, b) => a * b));
                case "MEDIAN":
                    return Median(args);
                case "LARGE":
                    return Kth(args, true);
                case "SMALL":
                    return Kth(args, false);
                case "RANK":
                case "RANK.EQ":
                    return Rank(args);
                case "SUMPRODUCT":
                    return SumProduct(args);
                case "SUBTOTAL":
                    return Subtotal(args);
                default:
                    return null;
            }
        }

        private ExcelValue NumberArgument(FormulaNode node, out double number)
        {
            if (node.Kind == FormulaNodeKind.Missing)
            {
                number = 0;
                return null;
            }

            return TryNumber(Scalar(node), out number, out var error) ? null : error;
        }

        private ExcelValue Mod(List<FormulaNode> args)
        {
            var problem = NumberArgument(args[0], out var number);
            if (problem != null)
                return problem;

            problem = NumberArgument(args[1], out var divisor);
            if (problem != null)
                return problem;

            if (divisor == 0)
                return ExcelValue.FromError(ExcelValue.DivideByZero);

            return ExcelValue.FromNumber(number - (divisor * Math.Floor(number / divisor)));
        }

        private ExcelValue Truncate(List<FormulaNode> args)
        {
            var problem = NumberArgument(args[0], out var number);
            if (problem != null)
                return problem;

            if (!TryInteger(args, 1, 0, out var digits, out var error))
                return error;

            return RoundValue(number, digits, MidpointRounding.ToZero);
        }

        private ExcelValue Median(List<FormulaNode> args)
        {
            var numbers = new List<double>();
            foreach (var arg in args)
            {
                var error = Collect(arg, numbers);
                if (error != null)
                    return error;
            }

            if (numbers.Count == 0)
                return ExcelValue.FromError(ExcelValue.BadNumber);

            numbers.Sort();
            var middle = numbers.Count / 2;
            return ExcelValue.FromNumber(numbers.Count % 2 == 1 ? numbers[middle] : (numbers[middle - 1] + numbers[middle]) / 2);
        }

        private ExcelValue Kth(List<FormulaNode> args, bool largest)
        {
            var numbers = new List<double>();
            var error = Collect(args[0], numbers);
            if (error != null)
                return error;

            var problem = NumberArgument(args[1], out var k);
            if (problem != null)
                return problem;

            var position = (int)Math.Ceiling(k);
            if (position < 1 || position > numbers.Count)
                return ExcelValue.FromError(ExcelValue.BadNumber);

            numbers.Sort();
            return ExcelValue.FromNumber(largest ? numbers[numbers.Count - position] : numbers[position - 1]);
        }

        private ExcelValue Rank(List<FormulaNode> args)
        {
            var problem = NumberArgument(args[0], out var number);
            if (problem != null)
                return problem;

            var reference = RangeArgument(args[1]);
            var numbers = new List<double>();
            foreach (var value in Values(reference))
            {
                if (value.IsError)
                    return value;

                if (value.IsNumber)
                    numbers.Add(ExcelValue.Round15(value.Number));
            }

            var order = 0.0;
            problem = args.Count > 2 ? NumberArgument(args[2], out order) : null;
            if (problem != null)
                return problem;

            var target = ExcelValue.Round15(number);
            if (!numbers.Contains(target))
                return ExcelValue.FromError(ExcelValue.NotAvailable);

            return ExcelValue.FromNumber(1 + (order == 0 ? numbers.Count(n => n > target) : numbers.Count(n => n < target)));
        }

        private ExcelValue SumProduct(List<FormulaNode> args)
        {
            var arrays = new List<ExcelValue[,]>();
            foreach (var arg in args)
            {
                if (arg.Kind == FormulaNodeKind.Missing)
                    return ExcelValue.FromError(ExcelValue.BadValue);

                arrays.Add(ArrayOf(arg));
            }

            var rows = arrays[0].GetLength(0);
            var columns = arrays[0].GetLength(1);
            if (arrays.Any(a => a.GetLength(0) != rows || a.GetLength(1) != columns))
                return ExcelValue.FromError(ExcelValue.BadValue);

            double total = 0;
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < columns; c++)
                {
                    double product = 1;
                    foreach (var array in arrays)
                    {
                        var value = array[r, c];
                        if (value.IsError)
                            return value;

                        product *= value.IsNumber ? value.Number : 0;
                    }

                    total += product;
                }
            }

            return ExcelValue.FromNumber(total);
        }

        private ExcelValue[,] ArrayOf(FormulaNode node)
        {
            switch (node.Kind)
            {
                case FormulaNodeKind.Unary:
                    return Map(ArrayOf(node.Children[0]), value => UnaryValue(node.Value, value));
                case FormulaNodeKind.Percent:
                    return Map(ArrayOf(node.Children[0]), PercentValue);
                case FormulaNodeKind.Binary when node.Value != " ":
                    return Combine(ArrayOf(node.Children[0]), ArrayOf(node.Children[1]), (left, right) => BinaryValues(node.Value, left, right));
            }

            var operand = Eval(node);
            return operand.IsRange ? Matrix(operand) : new[,] { { operand.Value } };
        }

        private ExcelValue[,] Matrix(FormulaOperand operand)
        {
            var range = operand.Range;
            var bottom = range.RowCount > FullColumnRows ? Math.Max(range.Top, Math.Min(range.Bottom, operand.Sheet.MaxRow)) : range.Bottom;
            var right = range.ColumnCount > FullRowColumns / 2 ? Math.Max(range.Left, Math.Min(range.Right, operand.Sheet.MaxColumn)) : range.Right;
            var result = new ExcelValue[bottom - range.Top + 1, right - range.Left + 1];
            for (var r = range.Top; r <= bottom; r++)
            {
                for (var c = range.Left; c <= right; c++)
                    result[r - range.Top, c - range.Left] = _calculator.GetValue(operand.Sheet, c, r);
            }

            return result;
        }

        private static ExcelValue[,] Map(ExcelValue[,] source, Func<ExcelValue, ExcelValue> map)
        {
            var result = new ExcelValue[source.GetLength(0), source.GetLength(1)];
            for (var r = 0; r < source.GetLength(0); r++)
            {
                for (var c = 0; c < source.GetLength(1); c++)
                    result[r, c] = map(source[r, c]);
            }

            return result;
        }

        private static ExcelValue[,] Combine(ExcelValue[,] left, ExcelValue[,] right, Func<ExcelValue, ExcelValue, ExcelValue> combine)
        {
            var leftSingle = left.GetLength(0) == 1 && left.GetLength(1) == 1;
            var rightSingle = right.GetLength(0) == 1 && right.GetLength(1) == 1;
            if (!leftSingle && !rightSingle && (left.GetLength(0) != right.GetLength(0) || left.GetLength(1) != right.GetLength(1)))
                return new[,] { { ExcelValue.FromError(ExcelValue.BadValue) } };

            var shape = leftSingle ? right : left;
            var result = new ExcelValue[shape.GetLength(0), shape.GetLength(1)];
            for (var r = 0; r < shape.GetLength(0); r++)
            {
                for (var c = 0; c < shape.GetLength(1); c++)
                    result[r, c] = combine(leftSingle ? left[0, 0] : left[r, c], rightSingle ? right[0, 0] : right[r, c]);
            }

            return result;
        }
    }
}
