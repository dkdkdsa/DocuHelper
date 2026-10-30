using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    internal sealed class FormulaEvaluator
    {
        private readonly WorkbookCalculator _calculator;
        private readonly ExcelSheet _sheet;
        private readonly int _column;
        private readonly int _row;

        public FormulaEvaluator(WorkbookCalculator calculator, ExcelSheet sheet, int column, int row)
        {
            _calculator = calculator;
            _sheet = sheet;
            _column = column;
            _row = row;
        }

        public ExcelValue Evaluate(FormulaNode node)
        {
            var value = Scalar(node);
            return value.IsBlank ? ExcelValue.Zero : value;
        }

        private FormulaOperand Eval(FormulaNode node)
        {
            switch (node.Kind)
            {
                case FormulaNodeKind.Number:
                    return FormulaOperand.Of(ExcelValue.FromNumber(node.Number));
                case FormulaNodeKind.Text:
                    return FormulaOperand.Of(ExcelValue.FromText(node.Value));
                case FormulaNodeKind.Boolean:
                    return FormulaOperand.Of(ExcelValue.FromBoolean(node.Number != 0));
                case FormulaNodeKind.Error:
                    return FormulaOperand.Of(ExcelValue.FromError(node.Value));
                case FormulaNodeKind.Missing:
                    return FormulaOperand.Of(ExcelValue.Blank);
                case FormulaNodeKind.Reference:
                    return Reference(node.Reference);
                case FormulaNodeKind.Unary:
                    return FormulaOperand.Of(Unary(node));
                case FormulaNodeKind.Percent:
                    return FormulaOperand.Of(Percent(node));
                case FormulaNodeKind.Binary:
                    return FormulaOperand.Of(Binary(node));
                case FormulaNodeKind.Function:
                    return FormulaOperand.Of(Call(node));
                default:
                    throw new UnknownValueException();
            }
        }

        private ExcelValue Scalar(FormulaNode node)
        {
            var operand = Eval(node);
            return operand.IsRange ? Intersect(operand) : operand.Value;
        }

        private FormulaOperand Reference(FormulaReference reference)
        {
            var sheet = reference.Sheet == null ? _sheet : _calculator.Book.FindSheet(reference.Sheet);
            if (sheet == null)
                return FormulaOperand.Of(ExcelValue.FromError(ExcelValue.BadReference));

            return FormulaOperand.Of(sheet, reference.ToRange(sheet.MaxRow, sheet.MaxColumn));
        }

        private ExcelValue Intersect(FormulaOperand operand)
        {
            var range = operand.Range;
            if (range.IsSingleCell)
                return _calculator.GetValue(operand.Sheet, range.Left, range.Top);

            if (range.Left == range.Right && _row >= range.Top && _row <= range.Bottom)
                return _calculator.GetValue(operand.Sheet, range.Left, _row);

            if (range.Top == range.Bottom && _column >= range.Left && _column <= range.Right)
                return _calculator.GetValue(operand.Sheet, _column, range.Top);

            return ExcelValue.FromError(ExcelValue.BadValue);
        }

        private IEnumerable<ExcelValue> Values(FormulaOperand operand)
        {
            var clipped = Clip(operand.Sheet, operand.Range);
            if (clipped == null)
                yield break;

            for (var row = clipped.Top; row <= clipped.Bottom; row++)
            {
                for (var column = clipped.Left; column <= clipped.Right; column++)
                    yield return _calculator.GetValue(operand.Sheet, column, row);
            }
        }

        private static CellRange Clip(ExcelSheet sheet, CellRange range)
        {
            if (sheet.MaxRow < 0)
                return null;

            return range.Intersect(new CellRange(0, 0, sheet.MaxColumn, sheet.MaxRow));
        }

        private ExcelValue Unary(FormulaNode node)
        {
            var value = Scalar(node.Children[0]);
            if (node.Value == "+")
                return value;

            return TryNumber(value, out var number, out var error) ? ExcelValue.FromNumber(-number) : error;
        }

        private ExcelValue Percent(FormulaNode node)
        {
            var value = Scalar(node.Children[0]);
            return TryNumber(value, out var number, out var error) ? ExcelValue.FromNumber(number / 100) : error;
        }

        private ExcelValue Binary(FormulaNode node)
        {
            var left = Scalar(node.Children[0]);
            var right = Scalar(node.Children[1]);
            var op = node.Value;
            if (op == "&")
            {
                if (left.IsError)
                    return left;

                if (right.IsError)
                    return right;

                return ExcelValue.FromText(ToText(left) + ToText(right));
            }

            if (op is "=" or "<>" or "<" or ">" or "<=" or ">=")
            {
                if (left.IsError)
                    return left;

                if (right.IsError)
                    return right;

                var comparison = Compare(left, right);
                return ExcelValue.FromBoolean(op switch
                {
                    "=" => comparison == 0,
                    "<>" => comparison != 0,
                    "<" => comparison < 0,
                    ">" => comparison > 0,
                    "<=" => comparison <= 0,
                    _ => comparison >= 0,
                });
            }

            if (!TryNumber(left, out var a, out var leftError))
                return leftError;

            if (!TryNumber(right, out var b, out var rightError))
                return rightError;

            switch (op)
            {
                case "+":
                    return ExcelValue.FromNumber(a + b);
                case "-":
                    return ExcelValue.FromNumber(a - b);
                case "*":
                    return ExcelValue.FromNumber(a * b);
                case "/":
                    return b == 0 ? ExcelValue.FromError(ExcelValue.DivideByZero) : ExcelValue.FromNumber(a / b);
                case "^":
                    if (a == 0 && b < 0)
                        return ExcelValue.FromError(ExcelValue.DivideByZero);

                    return ExcelValue.FromNumber(Math.Pow(a, b));
                default:
                    throw new UnknownValueException();
            }
        }

        private ExcelValue Call(FormulaNode node)
        {
            var args = node.Children;
            switch (FormulaFunctions.Normalize(node.Value))
            {
                case "SUM":
                    return Aggregate(args, numbers => numbers.Sum());
                case "AVERAGE":
                    return Aggregate(args, numbers => numbers.Count == 0 ? double.NaN : numbers.Average(), true);
                case "MIN":
                    return Aggregate(args, numbers => numbers.Count == 0 ? 0 : numbers.Min());
                case "MAX":
                    return Aggregate(args, numbers => numbers.Count == 0 ? 0 : numbers.Max());
                case "COUNT":
                    return Count(args, false);
                case "COUNTA":
                    return Count(args, true);
                case "ROUND":
                    return Round(args, MidpointRounding.AwayFromZero);
                case "ROUNDUP":
                    return Round(args, MidpointRounding.ToPositiveInfinity);
                case "ROUNDDOWN":
                    return Round(args, MidpointRounding.ToZero);
                case "IF":
                    return If(args);
                case "IFERROR":
                    var value = Scalar(args[0]);
                    return value.IsError ? Scalar(args[1]) : value;
                case "AND":
                    return Logical(args, true);
                case "OR":
                    return Logical(args, false);
                case "NOT":
                    return TryBoolean(Scalar(args[0]), out var flag, out var notError) ? ExcelValue.FromBoolean(!flag) : notError;
                case "ABS":
                    return TryNumber(Scalar(args[0]), out var absolute, out var absError) ? ExcelValue.FromNumber(Math.Abs(absolute)) : absError;
                case "SUMIF":
                    return SumIf(args, false);
                case "AVERAGEIF":
                    return SumIf(args, true);
                case "COUNTIF":
                    return CountIfs(args);
                case "COUNTIFS":
                    return CountIfs(args);
                case "SUMIFS":
                    return SumIfs(args);
                default:
                    throw new UnknownValueException();
            }
        }

        private ExcelValue Aggregate(List<FormulaNode> args, Func<List<double>, double> combine, bool average = false)
        {
            var numbers = new List<double>();
            foreach (var arg in args)
            {
                var error = Collect(arg, numbers);
                if (error != null)
                    return error;
            }

            if (average && numbers.Count == 0)
                return ExcelValue.FromError(ExcelValue.DivideByZero);

            return ExcelValue.FromNumber(combine(numbers));
        }

        private ExcelValue Collect(FormulaNode arg, List<double> numbers)
        {
            if (arg.Kind == FormulaNodeKind.Missing)
            {
                numbers.Add(0);
                return null;
            }

            var operand = Eval(arg);
            if (operand.IsRange)
            {
                foreach (var value in Values(operand))
                {
                    if (value.IsError)
                        return value;

                    if (value.IsNumber)
                        numbers.Add(value.Number);
                }

                return null;
            }

            var direct = operand.Value;
            if (direct.IsBlank)
                return null;

            if (!TryNumber(direct, out var number, out var error))
                return error;

            numbers.Add(number);
            return null;
        }

        private ExcelValue Count(List<FormulaNode> args, bool any)
        {
            var count = 0;
            foreach (var arg in args)
            {
                if (arg.Kind == FormulaNodeKind.Missing)
                {
                    count++;
                    continue;
                }

                var operand = Eval(arg);
                if (operand.IsRange)
                {
                    foreach (var value in Values(operand))
                    {
                        if (any ? !value.IsBlank : value.IsNumber)
                            count++;
                    }

                    continue;
                }

                var direct = operand.Value;
                if (any)
                {
                    count++;
                }
                else if (direct.IsNumber || direct.IsBoolean || (direct.IsText && TypedInput.TryParseNumber(direct.Text, out _, out _)))
                {
                    count++;
                }
            }

            return ExcelValue.FromNumber(count);
        }

        private ExcelValue Round(List<FormulaNode> args, MidpointRounding mode)
        {
            if (!TryNumber(Scalar(args[0]), out var number, out var error))
                return error;

            if (!TryNumber(Scalar(args[1]), out var digitsValue, out error))
                return error;

            var digits = (int)Math.Truncate(digitsValue);
            if (digits > 15 || Math.Abs(number) * Math.Pow(10, Math.Max(0, digits)) >= 7.9e27)
                return ExcelValue.FromNumber(number);

            if (digits < -15)
                return ExcelValue.Zero;

            var value = (decimal)number;
            var factor = 1m;
            for (var i = 0; i < Math.Abs(digits); i++)
                factor *= 10m;

            var scaled = digits >= 0 ? value * factor : value / factor;
            decimal rounded = mode switch
            {
                MidpointRounding.ToPositiveInfinity => Math.Sign(scaled) * Math.Ceiling(Math.Abs(scaled)),
                MidpointRounding.ToZero => Math.Truncate(scaled),
                _ => Math.Round(scaled, 0, MidpointRounding.AwayFromZero),
            };

            return ExcelValue.FromNumber((double)(digits >= 0 ? rounded / factor : rounded * factor));
        }

        private ExcelValue If(List<FormulaNode> args)
        {
            if (!TryBoolean(Scalar(args[0]), out var condition, out var error))
                return error;

            if (condition)
                return args.Count > 1 && args[1].Kind != FormulaNodeKind.Missing ? Scalar(args[1]) : ExcelValue.Zero;

            if (args.Count < 3)
                return ExcelValue.False;

            return args[2].Kind == FormulaNodeKind.Missing ? ExcelValue.Zero : Scalar(args[2]);
        }

        private ExcelValue Logical(List<FormulaNode> args, bool all)
        {
            var seen = false;
            var result = all;
            foreach (var arg in args)
            {
                if (arg.Kind == FormulaNodeKind.Missing)
                    continue;

                var operand = Eval(arg);
                var values = operand.IsRange ? Values(operand) : new[] { operand.Value };
                foreach (var value in values)
                {
                    if (value.IsError)
                        return value;

                    bool flag;
                    if (value.IsBoolean || value.IsNumber)
                    {
                        flag = value.IsBoolean ? value.Boolean : value.Number != 0;
                    }
                    else if (!operand.IsRange && value.IsText)
                    {
                        if (!TryBoolean(value, out flag, out var error))
                            return error;
                    }
                    else
                    {
                        continue;
                    }

                    seen = true;
                    result = all ? result && flag : result || flag;
                }
            }

            return seen ? ExcelValue.FromBoolean(result) : ExcelValue.FromError(ExcelValue.BadValue);
        }

        private ExcelValue SumIf(List<FormulaNode> args, bool average)
        {
            var range = Eval(args[0]);
            if (!range.IsRange)
                return ExcelValue.FromError(ExcelValue.BadValue);

            var criteria = FormulaCriteria.From(Scalar(args[1]));
            var target = args.Count > 2 && args[2].Kind != FormulaNodeKind.Missing ? Eval(args[2]) : range;
            if (!target.IsRange)
                return ExcelValue.FromError(ExcelValue.BadValue);

            var clipped = Clip(range.Sheet, range.Range);
            double total = 0;
            var count = 0;
            if (clipped != null)
            {
                for (var row = clipped.Top; row <= clipped.Bottom; row++)
                {
                    for (var column = clipped.Left; column <= clipped.Right; column++)
                    {
                        if (!criteria.Matches(_calculator.GetValue(range.Sheet, column, row)))
                            continue;

                        var value = _calculator.GetValue(target.Sheet, target.Range.Left + (column - range.Range.Left), target.Range.Top + (row - range.Range.Top));
                        if (value.IsError)
                            return value;

                        if (value.IsNumber)
                        {
                            total += value.Number;
                            count++;
                        }
                    }
                }
            }

            if (!average)
                return ExcelValue.FromNumber(total);

            return count == 0 ? ExcelValue.FromError(ExcelValue.DivideByZero) : ExcelValue.FromNumber(total / count);
        }

        private ExcelValue CountIfs(List<FormulaNode> args)
        {
            var pairs = new List<(FormulaOperand Range, FormulaCriteria Criteria)>();
            for (var i = 0; i + 1 < args.Count; i += 2)
            {
                var range = Eval(args[i]);
                if (!range.IsRange)
                    return ExcelValue.FromError(ExcelValue.BadValue);

                pairs.Add((range, FormulaCriteria.From(Scalar(args[i + 1]))));
            }

            if (pairs.Count == 0)
                return ExcelValue.Zero;

            var shape = pairs[0].Range.Range;
            if (pairs.Any(p => p.Range.Range.RowCount != shape.RowCount || p.Range.Range.ColumnCount != shape.ColumnCount))
                return ExcelValue.FromError(ExcelValue.BadValue);

            var usedRows = pairs.Max(p => Math.Min(p.Range.Range.Bottom, p.Range.Sheet.MaxRow) - p.Range.Range.Top + 1);
            var usedColumns = pairs.Max(p => Math.Min(p.Range.Range.Right, p.Range.Sheet.MaxColumn) - p.Range.Range.Left + 1);
            usedRows = Math.Clamp(usedRows, 0, shape.RowCount);
            usedColumns = Math.Clamp(usedColumns, 0, shape.ColumnCount);

            long count = 0;
            for (var dr = 0; dr < usedRows; dr++)
            {
                for (var dc = 0; dc < usedColumns; dc++)
                {
                    if (pairs.All(p => p.Criteria.Matches(_calculator.GetValue(p.Range.Sheet, p.Range.Range.Left + dc, p.Range.Range.Top + dr))))
                        count++;
                }
            }

            if (pairs.All(p => p.Criteria.Matches(ExcelValue.Blank)))
                count += shape.CellCount - ((long)usedRows * usedColumns);

            return ExcelValue.FromNumber(count);
        }

        private ExcelValue SumIfs(List<FormulaNode> args)
        {
            var target = Eval(args[0]);
            if (!target.IsRange)
                return ExcelValue.FromError(ExcelValue.BadValue);

            var pairs = new List<(FormulaOperand Range, FormulaCriteria Criteria)>();
            for (var i = 1; i + 1 < args.Count; i += 2)
            {
                var range = Eval(args[i]);
                if (!range.IsRange)
                    return ExcelValue.FromError(ExcelValue.BadValue);

                pairs.Add((range, FormulaCriteria.From(Scalar(args[i + 1]))));
            }

            var shape = target.Range;
            if (pairs.Any(p => p.Range.Range.RowCount != shape.RowCount || p.Range.Range.ColumnCount != shape.ColumnCount))
                return ExcelValue.FromError(ExcelValue.BadValue);

            var clipped = Clip(target.Sheet, shape);
            double total = 0;
            if (clipped == null)
                return ExcelValue.Zero;

            for (var row = clipped.Top; row <= clipped.Bottom; row++)
            {
                for (var column = clipped.Left; column <= clipped.Right; column++)
                {
                    var dr = row - shape.Top;
                    var dc = column - shape.Left;
                    if (!pairs.All(p => p.Criteria.Matches(_calculator.GetValue(p.Range.Sheet, p.Range.Range.Left + dc, p.Range.Range.Top + dr))))
                        continue;

                    var value = _calculator.GetValue(target.Sheet, column, row);
                    if (value.IsError)
                        return value;

                    if (value.IsNumber)
                        total += value.Number;
                }
            }

            return ExcelValue.FromNumber(total);
        }

        private static bool TryNumber(ExcelValue value, out double number, out ExcelValue error)
        {
            number = 0;
            error = null;
            switch (value.Kind)
            {
                case ExcelValueKind.Number:
                    number = value.Number;
                    return true;
                case ExcelValueKind.Blank:
                    return true;
                case ExcelValueKind.Boolean:
                    number = value.Boolean ? 1 : 0;
                    return true;
                case ExcelValueKind.Text:
                    if (TypedInput.TryParseNumber(value.Text, out number, out _))
                        return true;

                    error = ExcelValue.FromError(ExcelValue.BadValue);
                    return false;
                default:
                    error = value;
                    return false;
            }
        }

        private static bool TryBoolean(ExcelValue value, out bool flag, out ExcelValue error)
        {
            flag = false;
            error = null;
            switch (value.Kind)
            {
                case ExcelValueKind.Boolean:
                    flag = value.Boolean;
                    return true;
                case ExcelValueKind.Number:
                    flag = value.Number != 0;
                    return true;
                case ExcelValueKind.Blank:
                    return true;
                case ExcelValueKind.Text:
                    if (string.Equals(value.Text, "TRUE", StringComparison.OrdinalIgnoreCase))
                    {
                        flag = true;
                        return true;
                    }

                    if (string.Equals(value.Text, "FALSE", StringComparison.OrdinalIgnoreCase))
                        return true;

                    error = ExcelValue.FromError(ExcelValue.BadValue);
                    return false;
                default:
                    error = value;
                    return false;
            }
        }

        private static string ToText(ExcelValue value) => value.Kind switch
        {
            ExcelValueKind.Number => ExcelNumberFormat.FormatGeneral(value.Number),
            ExcelValueKind.Boolean => value.Boolean ? "TRUE" : "FALSE",
            ExcelValueKind.Text => value.Text,
            _ => string.Empty,
        };

        private static int Compare(ExcelValue left, ExcelValue right)
        {
            left = Normalize(left, right);
            right = Normalize(right, left);
            var leftRank = Rank(left);
            var rightRank = Rank(right);
            if (leftRank != rightRank)
                return leftRank.CompareTo(rightRank);

            return left.Kind switch
            {
                ExcelValueKind.Number => ExcelValue.Round15(left.Number).CompareTo(ExcelValue.Round15(right.Number)),
                ExcelValueKind.Text => Math.Sign(string.Compare(left.Text, right.Text, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase)),
                ExcelValueKind.Boolean => left.Boolean.CompareTo(right.Boolean),
                _ => 0,
            };
        }

        private static ExcelValue Normalize(ExcelValue value, ExcelValue other)
        {
            if (!value.IsBlank)
                return value;

            return other.Kind switch
            {
                ExcelValueKind.Text => ExcelValue.FromText(string.Empty),
                ExcelValueKind.Boolean => ExcelValue.False,
                _ => ExcelValue.Zero,
            };
        }

        private static int Rank(ExcelValue value) => value.Kind switch
        {
            ExcelValueKind.Number => 0,
            ExcelValueKind.Text => 1,
            ExcelValueKind.Boolean => 2,
            _ => 3,
        };
    }
}
