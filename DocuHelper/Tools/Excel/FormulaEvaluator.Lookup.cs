using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    internal sealed partial class FormulaEvaluator
    {
        private ExcelValue CallLookup(string name, List<FormulaNode> args) => name switch
        {
            "VLOOKUP" => TableLookup(args, true),
            "HLOOKUP" => TableLookup(args, false),
            "MATCH" => Match(args),
            _ => null,
        };

        private ExcelValue TableLookup(List<FormulaNode> args, bool vertical)
        {
            var lookup = Scalar(args[0]);
            if (lookup.IsError)
                return lookup;

            var table = RangeArgument(args[1]);
            if (!TryInteger(args, 2, 0, out var index, out var error))
                return error;

            if (index < 1)
                return ExcelValue.FromError(ExcelValue.BadValue);

            if (index > (vertical ? table.Range.ColumnCount : table.Range.RowCount))
                return ExcelValue.FromError(ExcelValue.BadReference);

            var approximate = true;
            if (args.Count > 3 && args[3].Kind != FormulaNodeKind.Missing && !TryBoolean(Scalar(args[3]), out approximate, out error))
                return error;

            var keys = Line(table, vertical);
            var position = approximate ? ApproximatePosition(keys, lookup, false) : ExactPosition(keys, lookup, true);
            if (position < 0)
                return ExcelValue.FromError(ExcelValue.NotAvailable);

            return vertical
                ? _calculator.GetValue(table.Sheet, table.Range.Left + index - 1, table.Range.Top + position)
                : _calculator.GetValue(table.Sheet, table.Range.Left + position, table.Range.Top + index - 1);
        }

        private ExcelValue Match(List<FormulaNode> args)
        {
            var lookup = Scalar(args[0]);
            if (lookup.IsError)
                return lookup;

            var array = RangeArgument(args[1]);
            if (array.Range.RowCount > 1 && array.Range.ColumnCount > 1)
                return ExcelValue.FromError(ExcelValue.NotAvailable);

            var type = 1.0;
            if (args.Count > 2 && args[2].Kind != FormulaNodeKind.Missing && !TryNumber(Scalar(args[2]), out type, out var error))
                return error;

            var keys = Line(array, array.Range.ColumnCount == 1);
            var position = type == 0 ? ExactPosition(keys, lookup, true) : ApproximatePosition(keys, lookup, type < 0);
            return position < 0 ? ExcelValue.FromError(ExcelValue.NotAvailable) : ExcelValue.FromNumber(position + 1);
        }

        private FormulaOperand Index(List<FormulaNode> args)
        {
            var array = RangeArgument(args[0]);
            var range = array.Range;
            if (!TryInteger(args, 1, 0, out var row, out var error))
                return FormulaOperand.Of(error);

            var hasColumn = args.Count > 2 && args[2].Kind != FormulaNodeKind.Missing;
            var column = 0;
            if (hasColumn && !TryInteger(args, 2, 0, out column, out error))
                return FormulaOperand.Of(error);

            if (args.Count > 3 && args[3].Kind != FormulaNodeKind.Missing)
            {
                if (!TryInteger(args, 3, 1, out var area, out error))
                    return FormulaOperand.Of(error);

                if (area != 1)
                    return FormulaOperand.Of(ExcelValue.FromError(ExcelValue.BadReference));
            }

            if (!hasColumn)
            {
                if (range.RowCount == 1 && range.ColumnCount > 1)
                {
                    column = row;
                    row = 1;
                }
                else if (range.ColumnCount == 1)
                {
                    column = 1;
                }
            }

            if (row < 0 || column < 0)
                return FormulaOperand.Of(ExcelValue.FromError(ExcelValue.BadValue));

            if (row > range.RowCount || column > range.ColumnCount)
                return FormulaOperand.Of(ExcelValue.FromError(ExcelValue.BadReference));

            var top = row == 0 ? range.Top : range.Top + row - 1;
            var bottom = row == 0 ? range.Bottom : top;
            var left = column == 0 ? range.Left : range.Left + column - 1;
            var right = column == 0 ? range.Right : left;
            return FormulaOperand.Of(array.Sheet, new CellRange(left, top, right, bottom));
        }

        private FormulaOperand XLookup(List<FormulaNode> args)
        {
            var lookup = Scalar(args[0]);
            if (lookup.IsError)
                return FormulaOperand.Of(lookup);

            var keys = RangeArgument(args[1]);
            var results = RangeArgument(args[2]);
            if (keys.Range.RowCount > 1 && keys.Range.ColumnCount > 1)
                return FormulaOperand.Of(ExcelValue.FromError(ExcelValue.BadValue));

            var vertical = keys.Range.ColumnCount == 1 && (keys.Range.RowCount > 1 || results.Range.RowCount == 1);
            if (vertical ? results.Range.RowCount != keys.Range.RowCount : results.Range.ColumnCount != keys.Range.ColumnCount)
                return FormulaOperand.Of(ExcelValue.FromError(ExcelValue.BadValue));

            if (!TryInteger(args, 4, 0, out var matchMode, out var error) || !TryInteger(args, 5, 1, out var searchMode, out error))
                return FormulaOperand.Of(error);

            if (matchMode < -1 || matchMode > 2 || searchMode == 0 || Math.Abs(searchMode) > 2)
                return FormulaOperand.Of(ExcelValue.FromError(ExcelValue.BadValue));

            var values = Line(keys, vertical);
            var order = searchMode > 0 ? Enumerable.Range(0, values.Count) : Enumerable.Range(0, values.Count).Reverse();
            var found = FindInOrder(values, order.ToList(), lookup, matchMode);
            if (found < 0)
            {
                if (args.Count > 3 && args[3].Kind != FormulaNodeKind.Missing)
                    return Eval(args[3]);

                return FormulaOperand.Of(ExcelValue.FromError(ExcelValue.NotAvailable));
            }

            var range = results.Range;
            return vertical
                ? FormulaOperand.Of(results.Sheet, new CellRange(range.Left, range.Top + found, range.Right, range.Top + found))
                : FormulaOperand.Of(results.Sheet, new CellRange(range.Left + found, range.Top, range.Left + found, range.Bottom));
        }

        private static int FindInOrder(List<ExcelValue> values, List<int> order, ExcelValue lookup, int matchMode)
        {
            if (lookup.IsBlank)
                return -1;

            var criteria = FormulaCriteria.Exact(lookup, matchMode == 2);
            foreach (var i in order)
            {
                if (criteria.Matches(values[i]))
                    return i;
            }

            if (matchMode is 0 or 2)
                return -1;

            var best = -1;
            foreach (var i in order)
            {
                var value = values[i];
                if (!SameKind(value, lookup))
                    continue;

                var comparison = Compare(value, lookup);
                if (matchMode < 0 ? comparison >= 0 : comparison <= 0)
                    continue;

                if (best < 0 || (matchMode < 0 ? Compare(value, values[best]) > 0 : Compare(value, values[best]) < 0))
                    best = i;
            }

            return best;
        }

        private FormulaOperand RangeArgument(FormulaNode node)
        {
            var operand = Eval(node);
            if (!operand.IsRange)
                throw new UnknownValueException();

            return operand;
        }

        private List<ExcelValue> Line(FormulaOperand operand, bool vertical)
        {
            var range = operand.Range;
            var values = new List<ExcelValue>();
            if (vertical)
            {
                var bottom = Math.Min(range.Bottom, operand.Sheet.MaxRow);
                for (var row = range.Top; row <= bottom; row++)
                    values.Add(_calculator.GetValue(operand.Sheet, range.Left, row));
            }
            else
            {
                var right = Math.Min(range.Right, operand.Sheet.MaxColumn);
                for (var column = range.Left; column <= right; column++)
                    values.Add(_calculator.GetValue(operand.Sheet, column, range.Top));
            }

            return values;
        }

        private static int ExactPosition(List<ExcelValue> values, ExcelValue lookup, bool wildcards)
        {
            if (lookup.IsBlank)
                return -1;

            var criteria = FormulaCriteria.Exact(lookup, wildcards);
            for (var i = 0; i < values.Count; i++)
            {
                if (criteria.Matches(values[i]))
                    return i;
            }

            return -1;
        }

        private static int ApproximatePosition(List<ExcelValue> values, ExcelValue lookup, bool descending)
        {
            var best = -1;
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (!SameKind(value, lookup))
                    continue;

                var comparison = Compare(value, lookup);
                if (descending ? comparison >= 0 : comparison <= 0)
                    best = i;
                else
                    break;
            }

            return best;
        }

        private static bool SameKind(ExcelValue value, ExcelValue lookup) =>
            !value.IsBlank && !value.IsError && value.Kind == lookup.Kind;

        private bool TryInteger(List<FormulaNode> args, int index, int fallback, out int value, out ExcelValue error)
        {
            value = fallback;
            error = null;
            if (index >= args.Count || args[index].Kind == FormulaNodeKind.Missing)
                return true;

            if (!TryNumber(Scalar(args[index]), out var number, out error))
                return false;

            value = (int)Math.Clamp(Math.Truncate(number), int.MinValue, int.MaxValue);
            return true;
        }
    }
}
