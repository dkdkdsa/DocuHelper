using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed partial class ExcelSheet
    {
        public const int MaxOutlineLevel = 7;

        public bool SummaryBelow => Worksheet?.SheetProperties?.OutlineProperties?.SummaryBelow?.Value ?? true;

        public bool SummaryRight => Worksheet?.SheetProperties?.OutlineProperties?.SummaryRight?.Value ?? true;

        public int RowOutline(int row) => (int)(FindRow(row)?.OutlineLevel?.Value ?? 0);

        public int ColumnOutline(int column)
        {
            var definition = Worksheet?.GetFirstChild<Columns>()?.Elements<Column>()
                .FirstOrDefault(c => (c.Min?.Value ?? 0) <= column + 1 && (c.Max?.Value ?? 0) >= column + 1);
            return (int)(definition?.OutlineLevel?.Value ?? 0);
        }

        internal void SetRowOutline(int row, int level)
        {
            if (level <= 0)
            {
                var existing = FindRow(row);
                if (existing != null)
                    existing.OutlineLevel = null;

                return;
            }

            GetOrCreateRow(row).OutlineLevel = (byte)level;
        }

        internal void SetRowCollapsed(int row, bool collapsed)
        {
            if (!collapsed)
            {
                var existing = FindRow(row);
                if (existing != null)
                    existing.Collapsed = null;

                return;
            }

            GetOrCreateRow(row).Collapsed = true;
        }

        internal void SetColumnOutline(int column, int level)
        {
            if (level <= 0)
            {
                EditColumns(column, column, false, c => c.OutlineLevel = null);
                return;
            }

            EditColumns(column, column, true, c => c.OutlineLevel = (byte)level);
        }

        internal void SetColumnCollapsed(int column, bool collapsed)
        {
            if (!collapsed)
            {
                EditColumns(column, column, false, c => c.Collapsed = null);
                return;
            }

            EditColumns(column, column, true, c => c.Collapsed = true);
        }

        internal void SetSummary(bool? below, bool? right)
        {
            var worksheet = Worksheet;
            var properties = worksheet.SheetProperties;
            if (properties == null)
            {
                properties = new SheetProperties();
                worksheet.SheetProperties = properties;
            }

            var outline = properties.OutlineProperties;
            if (outline == null)
            {
                outline = new OutlineProperties();
                properties.OutlineProperties = outline;
            }

            if (below.HasValue)
                outline.SummaryBelow = below.Value;

            if (right.HasValue)
                outline.SummaryRight = right.Value;
        }

        internal void SyncOutlineLevels()
        {
            var worksheet = Worksheet;
            var rowLevel = Data?.Elements<Row>().Select(r => (int)(r.OutlineLevel?.Value ?? 0)).DefaultIfEmpty(0).Max() ?? 0;
            var columnLevel = worksheet.GetFirstChild<Columns>()?.Elements<Column>().Select(c => (int)(c.OutlineLevel?.Value ?? 0)).DefaultIfEmpty(0).Max() ?? 0;
            var format = worksheet.SheetFormatProperties;
            if (format == null)
            {
                if (rowLevel == 0 && columnLevel == 0)
                    return;

                format = WorksheetOrder.Insert(worksheet, new SheetFormatProperties { DefaultRowHeight = 15D });
            }

            format.OutlineLevelRow = rowLevel > 0 ? (byte)rowLevel : null;
            format.OutlineLevelColumn = columnLevel > 0 ? (byte)columnLevel : null;
        }
    }
}
