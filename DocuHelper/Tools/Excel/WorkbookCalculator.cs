using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed class WorkbookCalculator
    {
        private const long ReadBudget = 4_000_000;

        private readonly HashSet<CellKey> _written;
        private readonly Dictionary<CellKey, FormulaCellState> _states = new();
        private readonly Dictionary<string, FormulaNode> _parsed = new(StringComparer.Ordinal);
        private readonly Dictionary<int, List<ExcelCellEntry>> _formulas = new();
        private long _reads;

        public WorkbookCalculator(ExcelBook book, IEnumerable<CellKey> written)
        {
            Book = book;
            _written = new HashSet<CellKey>(written ?? Enumerable.Empty<CellKey>());
        }

        public ExcelBook Book { get; }

        public CalculationResult Run()
        {
            var result = new CalculationResult();
            foreach (var sheet in Book.Sheets)
                _formulas[sheet.Index] = sheet.Entries.Where(e => e.Cell.CellFormula != null).ToList();

            try
            {
                foreach (var sheet in Book.Sheets)
                {
                    foreach (var entry in _formulas[sheet.Index])
                        Resolve(sheet, entry.Column, entry.Row, entry.Cell);
                }
            }
            catch (CalculationBudgetException)
            {
                result.Aborted = true;
                foreach (var key in _written)
                {
                    var cell = Book.Sheets[key.SheetIndex].FindCell(key.Column, key.Row);
                    if (cell?.CellFormula == null)
                        continue;

                    ClearCached(cell);
                    result.Unknown.Add(key);
                }

                return result;
            }

            foreach (var pair in _states)
            {
                var key = pair.Key;
                var state = pair.Value;
                var cell = Book.Sheets[key.SheetIndex].FindCell(key.Column, key.Row);
                if (cell == null)
                    continue;

                switch (state.Status)
                {
                    case FormulaCellState.Known:
                        if (WriteCached(cell, state.Value) && !_written.Contains(key))
                            result.Updated++;

                        result.Values[key] = state.Value;
                        break;
                    case FormulaCellState.Cached:
                        result.Values[key] = state.Value;
                        break;
                    default:
                        if (cell.CellValue != null && !_written.Contains(key))
                            result.Updated++;

                        ClearCached(cell);
                        result.Unknown.Add(key);
                        break;
                }
            }

            return result;
        }

        internal ExcelValue GetValue(ExcelSheet sheet, int column, int row)
        {
            if (++_reads > ReadBudget)
                throw new CalculationBudgetException();

            var cell = sheet.FindCell(column, row);
            if (cell == null)
                return ExcelValue.Blank;

            if (cell.CellFormula != null)
            {
                var state = Resolve(sheet, column, row, cell);
                if (state == null)
                    return Book.ReadValue(cell);

                if (!state.HasValue)
                    throw new UnknownValueException();

                return state.Value;
            }

            return Book.ReadValue(cell);
        }

        private FormulaCellState Resolve(ExcelSheet sheet, int column, int row, Cell cell)
        {
            var key = new CellKey(sheet.Index, column, row);
            if (_states.TryGetValue(key, out var existing))
                return existing;

            var text = sheet.FormulaOf(cell, column, row);
            if (string.IsNullOrEmpty(text))
                return null;

            var state = new FormulaCellState { Status = FormulaCellState.Evaluating };
            _states[key] = state;

            var cached = cell.CellValue != null ? Book.ReadValue(cell) : null;
            var node = Parse(text);
            var type = cell.CellFormula.FormulaType?.InnerText;
            var supported = node != null && (type == null || type == "normal" || type == "shared");
            var written = _written.Contains(key);

            ExcelValue value = null;
            if (supported)
            {
                try
                {
                    value = new FormulaEvaluator(this, sheet, column, row).Evaluate(node);
                }
                catch (UnknownValueException)
                {
                    value = null;
                }
            }

            if (value != null)
            {
                state.Status = FormulaCellState.Known;
                state.Value = value;
                state.Changed = written || cached == null || !value.SameAs(cached);
                return state;
            }

            var changed = written || (node != null && AnyPrecedentChanged(node, sheet));
            if (!changed && cached != null)
            {
                state.Status = FormulaCellState.Cached;
                state.Value = cached;
                state.Changed = false;
            }
            else
            {
                state.Status = FormulaCellState.Unknown;
                state.Changed = changed;
            }

            return state;
        }

        private FormulaNode Parse(string text)
        {
            if (_parsed.TryGetValue(text, out var node))
                return node;

            FormulaParser.TryParse(text, out node);
            _parsed[text] = node;
            return node;
        }

        private bool AnyPrecedentChanged(FormulaNode node, ExcelSheet sheet)
        {
            foreach (var reference in node.Descendants().Where(n => n.Kind == FormulaNodeKind.Reference).Select(n => n.Reference))
            {
                var target = reference.Sheet == null ? sheet : Book.FindSheet(reference.Sheet);
                if (target == null)
                    continue;

                var range = reference.ToFullRange();
                foreach (var key in _written)
                {
                    if (key.SheetIndex == target.Index && range.Contains(key.Column, key.Row))
                        return true;
                }

                foreach (var entry in _formulas[target.Index])
                {
                    if (!range.Contains(entry.Column, entry.Row))
                        continue;

                    var state = Resolve(target, entry.Column, entry.Row, entry.Cell);
                    if (state != null && state.Changed)
                        return true;
                }
            }

            return false;
        }

        private static bool WriteCached(Cell cell, ExcelValue value)
        {
            string type;
            string text;
            switch (value.Kind)
            {
                case ExcelValueKind.Text:
                    type = "str";
                    text = value.Text;
                    break;
                case ExcelValueKind.Boolean:
                    type = "b";
                    text = value.Boolean ? "1" : "0";
                    break;
                case ExcelValueKind.Error:
                    type = "e";
                    text = value.Text;
                    break;
                case ExcelValueKind.Number:
                    type = null;
                    text = ExcelValue.Invariant(value.Number);
                    break;
                default:
                    type = null;
                    text = "0";
                    break;
            }

            var current = cell.DataType?.InnerText;
            if (current == "n")
                current = null;

            if (current == type && cell.CellValue?.Text == text)
                return false;

            if (type == "str")
                cell.DataType = CellValues.String;
            else if (type == "b")
                cell.DataType = CellValues.Boolean;
            else if (type == "e")
                cell.DataType = CellValues.Error;
            else
                cell.DataType = null;

            cell.CellValue = new CellValue(text);
            return true;
        }

        private static void ClearCached(Cell cell)
        {
            if (cell.CellValue != null)
                cell.CellValue = null;

            if (cell.DataType != null)
                cell.DataType = null;
        }
    }
}
