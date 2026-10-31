using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelTable
    {
        internal ExcelTable(ExcelSheet sheet, TableDefinitionPart part, CellRange range)
        {
            Sheet = sheet;
            Part = part;
            Range = range;
        }

        public ExcelSheet Sheet { get; }

        public CellRange Range { get; }

        public Table Definition => Part.Table;

        public string Name => Definition.DisplayName?.Value ?? Definition.Name?.Value ?? string.Empty;

        public bool HasHeader => (Definition.HeaderRowCount?.Value ?? 1) > 0;

        public bool HasTotals => (Definition.TotalsRowCount?.Value ?? 0) > 0;

        public CellRange HeaderRange => HasHeader ? new CellRange(Range.Left, Range.Top, Range.Right, Range.Top) : null;

        public CellRange DataRange
        {
            get
            {
                var top = HasHeader ? Range.Top + 1 : Range.Top;
                var bottom = HasTotals ? Range.Bottom - 1 : Range.Bottom;
                return bottom >= top ? new CellRange(Range.Left, top, Range.Right, bottom) : null;
            }
        }

        public CellRange TotalsRange => HasTotals ? new CellRange(Range.Left, Range.Bottom, Range.Right, Range.Bottom) : null;

        public IReadOnlyList<string> Columns =>
            Definition.TableColumns?.Elements<TableColumn>().Select(c => c.Name?.Value ?? string.Empty).ToList() ?? new List<string>();

        public string StyleName => Definition.TableStyleInfo?.Name?.Value;

        public bool BandedRows => Definition.TableStyleInfo?.ShowRowStripes?.Value == true;

        public bool BandedColumns => Definition.TableStyleInfo?.ShowColumnStripes?.Value == true;

        public bool FirstColumn => Definition.TableStyleInfo?.ShowFirstColumn?.Value == true;

        public bool LastColumn => Definition.TableStyleInfo?.ShowLastColumn?.Value == true;

        public bool FilterButtons => HasHeader && Definition.AutoFilter != null;

        public bool IsLinked =>
            (Definition.TableType?.Value != null && Definition.TableType.Value != TableValues.Worksheet)
            || Definition.ConnectionId != null
            || Part.QueryTableParts.Any();

        internal TableDefinitionPart Part { get; }

        public bool Contains(int column, int row) => Range.Contains(column, row);

        public int ColumnIndex(int column) => column - Range.Left;
    }
}
