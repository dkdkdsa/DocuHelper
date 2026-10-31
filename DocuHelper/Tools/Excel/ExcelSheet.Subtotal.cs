using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed partial class ExcelSheet
    {
        internal bool ContainsSubtotal(Cell cell)
        {
            var formula = cell?.CellFormula;
            if (formula == null)
                return false;

            var text = formula.Text;
            if (string.IsNullOrEmpty(text))
            {
                if (formula.FormulaType?.InnerText != "shared" || formula.SharedIndex == null)
                    return false;

                _sharedMasters ??= BuildSharedMasters();
                if (!_sharedMasters.TryGetValue(formula.SharedIndex.Value, out var master))
                    return false;

                text = master.Text;
            }

            return CallsSubtotal(text);
        }

        internal IReadOnlyList<CellRange> FilteredRows()
        {
            var spans = new List<CellRange>();
            AddFilteredRows(spans, Worksheet?.GetFirstChild<AutoFilter>());
            foreach (var part in Part.TableDefinitionParts)
                AddFilteredRows(spans, part.Table?.AutoFilter);

            return spans;
        }

        private static void AddFilteredRows(List<CellRange> spans, AutoFilter filter)
        {
            if (filter == null || !filter.Elements<FilterColumn>().Any())
                return;

            if (CellRange.TryParse(filter.Reference?.Value, out var range) && range.RowCount > 1)
                spans.Add(new CellRange(range.Left, range.Top + 1, range.Right, range.Bottom));
        }

        private static bool CallsSubtotal(string formula)
        {
            if (formula.IndexOf("SUBTOTAL", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            try
            {
                return FormulaLexer.Tokenize(formula).Any(t => t.Kind == FormulaTokenKind.Function && FormulaFunctions.Normalize(t.Value) == "SUBTOTAL");
            }
            catch (FormulaException)
            {
                return true;
            }
        }
    }
}
