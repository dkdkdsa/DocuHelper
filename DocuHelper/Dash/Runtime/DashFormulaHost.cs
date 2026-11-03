using System;
using System.Collections.Generic;
using DocuHelper.Docus;
using DocuHelper.Tools;
using DocuHelper.Tools.Excel;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Dash.Runtime
{
    internal sealed class DashFormulaHost : IDisposable
    {
        private const string ValuesSheet = "값";
        private const int ConstantColumn = 0;
        private const int FormulaColumn = 1;

        private readonly ExcelDocu _docu;
        private readonly ExcelBook _book;
        private readonly ExcelSheet _values;
        private readonly Dictionary<string, Cell> _results = new(StringComparer.Ordinal);
        private readonly List<CellKey> _written = new();
        private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
        private int _constants;
        private int _formulas;
        private int _tables;

        public DashFormulaHost()
        {
            _docu = new ExcelDocu("dash-calc.xlsx", BlankWorkbook.Create(ValuesSheet));
            _book = new ExcelBook(_docu.Document);
            _values = _book.FindSheet(ValuesSheet);
        }

        public bool HasName(string name) => name != null && _names.Contains(name);

        public void Constant(string name, object value)
        {
            var row = _constants++;
            Put(_values.GetOrCreateCell(ConstantColumn, row), value);
            Name(name, _values.Name, ConstantColumn, row, ConstantColumn, row);
        }

        public string Table(string name, DashTable table)
        {
            _tables++;
            var sheet = _book.AddSheet("t" + _tables.ToString(System.Globalization.CultureInfo.InvariantCulture), null);
            for (var column = 0; column < table.Columns.Count; column++)
                _book.SetText(sheet.GetOrCreateCell(column, 0), table.Columns[column]);

            for (var row = 0; row < table.Rows.Count; row++)
            {
                var values = table.Rows[row];
                for (var column = 0; column < table.Columns.Count; column++)
                    Put(sheet.GetOrCreateCell(column, row + 1), DashTable.Get(values, column));
            }

            var last = Math.Max(1, table.Rows.Count);
            if (name != null)
            {
                for (var column = 0; column < table.Columns.Count; column++)
                    Name(name + "." + table.Columns[column], sheet.Name, column, 1, column, last);

                if (table.Columns.Count > 0)
                    Name(name, sheet.Name, 0, 1, table.Columns.Count - 1, last);
            }

            return sheet.Name;
        }

        public void Formula(string key, string expression, string name = null)
        {
            var row = _formulas++;
            var cell = _values.GetOrCreateCell(FormulaColumn, row);
            var text = DashFormulaText.Strip(expression);
            if (string.IsNullOrWhiteSpace(text))
                text = "\"\"";

            ExcelBook.SetFormula(cell, text);
            _written.Add(new CellKey(_values.Index, FormulaColumn, row));
            _results[key] = cell;
            Name(name, _values.Name, FormulaColumn, row, FormulaColumn, row);
        }

        public void Run() => new WorkbookCalculator(_book, _written, strict: true).Run();

        public object Result(string key) =>
            _results.TryGetValue(key, out var cell) ? DashCells.FromExcel(_book.ReadValue(cell), false) : null;

        public void Dispose() => _docu.Dispose();

        private void Put(Cell cell, object value)
        {
            switch (value)
            {
                case null:
                    return;
                case string text:
                    _book.SetText(cell, text);
                    return;
                case bool flag:
                    ExcelBook.SetBoolean(cell, flag);
                    return;
                case DateTime date:
                    ExcelBook.SetNumber(cell, date.ToOADate());
                    return;
                case DashError error:
                    ExcelBook.SetFormula(cell, error.Code == ExcelValue.NotAvailable ? "NA()" : "1/0");
                    return;
                case DashRange range:
                    ExcelBook.SetNumber(cell, range.From);
                    return;
            }

            var number = DashCells.Number(value);
            if (number.HasValue)
                ExcelBook.SetNumber(cell, number.Value);
            else
                _book.SetText(cell, DashCells.Text(value));
        }

        private void Name(string name, string sheet, int firstColumn, int firstRow, int lastColumn, int lastRow)
        {
            if (string.IsNullOrWhiteSpace(name) || _names.Contains(name))
                return;

            try
            {
                ExcelNames.Check(_book, name, null, null);
            }
            catch (ToolInputException)
            {
                return;
            }

            _names.Add(name);
            var start = "$" + CellAddress.ColumnName(firstColumn) + "$" + (firstRow + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var reference = firstColumn == lastColumn && firstRow == lastRow
                ? start
                : start + ":$" + CellAddress.ColumnName(lastColumn) + "$" + (lastRow + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            ExcelNames.Add(_book, name, null, "'" + sheet.Replace("'", "''") + "'!" + reference);
        }
    }
}
