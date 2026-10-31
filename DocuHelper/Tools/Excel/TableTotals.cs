using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal static class TableTotals
    {
        public static void Show(ExcelBook book, ExcelTable table)
        {
            var sheet = table.Sheet;
            var range = table.Range;
            var row = range.Bottom + 1;
            var columns = table.Definition.TableColumns.Elements<TableColumn>().ToList();
            var summed = SummedColumn(book, table);
            sheet.PrepareForWrite();

            if (summed != 0)
            {
                columns[0].TotalsRowLabel = ExcelTables.TotalLabel;
                book.SetText(sheet.GetOrCreateCell(range.Left, row), ExcelTables.TotalLabel);
            }

            if (summed >= 0)
            {
                var column = columns[summed];
                column.TotalsRowFunction = TotalsRowFunctionValues.Sum;
                var cell = sheet.GetOrCreateCell(range.Left + summed, row);
                cell.CellFormula = new CellFormula("SUBTOTAL(109," + StructuredReferences.Column(column.Name?.Value ?? string.Empty) + ")");
                cell.InlineString = null;
                cell.DataType = null;
                cell.CellValue = new CellValue("0");
                var above = sheet.FindCell(range.Left + summed, range.Bottom);
                var format = above == null ? null : book.StyleOf(above);
                if (format != null && !format.IsGeneral && !format.IsDate && !format.IsText)
                {
                    var index = book.Styles.Apply(cell.StyleIndex?.Value ?? 0, new StyleChange { NumberFormatId = format.NumberFormatId });
                    cell.StyleIndex = index == 0 ? null : index;
                }
            }

            table.Definition.TotalsRowCount = 1;
            table.Definition.TotalsRowShown = null;
            table.Definition.Reference = new CellRange(range.Left, range.Top, range.Right, row).ToString();
            sheet.UpdateDimension();
            Refresh(book);
        }

        public static void Hide(ExcelBook book, ExcelTable table)
        {
            var sheet = table.Sheet;
            var range = table.Range;
            sheet.PrepareForWrite();
            for (var column = range.Left; column <= range.Right; column++)
                sheet.ClearContent(column, range.Bottom);

            foreach (var column in table.Definition.TableColumns.Elements<TableColumn>())
            {
                column.TotalsRowLabel = null;
                column.TotalsRowFunction = null;
                column.TotalsRowFormula = null;
            }

            table.Definition.TotalsRowCount = null;
            table.Definition.TotalsRowShown = false;
            table.Definition.Reference = new CellRange(range.Left, range.Top, range.Right, range.Bottom - 1).ToString();
            sheet.UpdateDimension();
        }

        public static void Refresh(ExcelBook book)
        {
            foreach (var table in ExcelTables.All(book))
            {
                if (!table.HasTotals || table.DataRange == null)
                    continue;

                var columns = table.Definition.TableColumns?.Elements<TableColumn>().ToList();
                if (columns == null || columns.Count != table.Range.ColumnCount)
                    continue;

                var sheet = table.Sheet;
                for (var i = 0; i < columns.Count; i++)
                {
                    var code = Code(columns[i].TotalsRowFunction?.Value);
                    if (code == 0)
                        continue;

                    var cell = sheet.FindCell(table.Range.Left + i, table.Range.Bottom);
                    var formula = cell?.CellFormula?.Text;
                    if (formula == null || !formula.TrimStart().StartsWith("SUBTOTAL(", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var value = Compute(book, sheet, table.DataRange, table.Range.Left + i, code);
                    var current = book.ReadValue(cell);
                    if (current.SameAs(value))
                        continue;

                    if (value.IsError)
                    {
                        cell.DataType = CellValues.Error;
                        cell.CellValue = new CellValue(value.Text);
                    }
                    else
                    {
                        cell.DataType = null;
                        cell.CellValue = new CellValue(ExcelValue.Invariant(value.Number));
                    }
                }
            }
        }

        private static int Code(TotalsRowFunctionValues? function)
        {
            if (function == TotalsRowFunctionValues.Sum)
                return 109;

            if (function == TotalsRowFunctionValues.Average)
                return 101;

            if (function == TotalsRowFunctionValues.Count)
                return 103;

            if (function == TotalsRowFunctionValues.CountNumbers)
                return 102;

            if (function == TotalsRowFunctionValues.Maximum)
                return 104;

            if (function == TotalsRowFunctionValues.Minimum)
                return 105;

            return 0;
        }

        private static ExcelValue Compute(ExcelBook book, ExcelSheet sheet, CellRange data, int column, int code)
        {
            var numbers = new List<double>();
            var filled = 0;
            for (var row = data.Top; row <= data.Bottom; row++)
            {
                if (sheet.IsRowHidden(row))
                    continue;

                var cell = sheet.FindCell(column, row);
                var value = book.ReadValue(cell);
                if (value.IsBlank)
                    continue;

                filled++;
                if (value.IsNumber)
                    numbers.Add(value.Number);
            }

            switch (code)
            {
                case 101:
                    return numbers.Count == 0 ? ExcelValue.FromError(ExcelValue.DivideByZero) : ExcelValue.FromNumber(numbers.Sum() / numbers.Count);
                case 102:
                    return ExcelValue.FromNumber(numbers.Count);
                case 103:
                    return ExcelValue.FromNumber(filled);
                case 104:
                    return ExcelValue.FromNumber(numbers.Count == 0 ? 0 : numbers.Max());
                case 105:
                    return ExcelValue.FromNumber(numbers.Count == 0 ? 0 : numbers.Min());
                default:
                    return ExcelValue.FromNumber(numbers.Sum());
            }
        }

        private static int SummedColumn(ExcelBook book, ExcelTable table)
        {
            var data = table.DataRange;
            if (data == null)
                return -1;

            var numbers = new int[data.ColumnCount];
            var texts = new int[data.ColumnCount];
            foreach (var entry in table.Sheet.Entries)
            {
                if (!data.Contains(entry.Column, entry.Row))
                    continue;

                var value = book.ReadValue(entry.Cell);
                var index = entry.Column - data.Left;
                if (value.IsNumber)
                    numbers[index]++;
                else if (!value.IsBlank)
                    texts[index]++;
            }

            for (var i = numbers.Length - 1; i >= 0; i--)
            {
                if (numbers[i] > 0 && texts[i] == 0)
                    return i;
            }

            return -1;
        }
    }
}
