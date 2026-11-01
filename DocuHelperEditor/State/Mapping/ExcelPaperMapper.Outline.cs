using System;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        static partial void MapOutline(ExcelFeatureContext context)
        {
            var view = context.View;
            var properties = context.Source.Worksheet?.SheetProperties?.OutlineProperties;
            view.SummaryBelow = properties?.SummaryBelow?.Value ?? true;
            view.SummaryRight = properties?.SummaryRight?.Value ?? true;
            var levels = 0;
            foreach (var element in context.Source.Data?.Elements<DocumentFormat.OpenXml.Spreadsheet.Row>() ?? Enumerable.Empty<DocumentFormat.OpenXml.Spreadsheet.Row>())
            {
                var level = (int)(element.OutlineLevel?.Value ?? 0);
                var collapsed = element.Collapsed?.Value == true;
                if (level == 0 && !collapsed)
                    continue;

                var index = (int)(element.RowIndex?.Value ?? 1) - 1;
                if (index < 0 || index >= view.Rows.Count)
                    continue;

                var target = view.Rows.Ensure(index);
                target.OutlineLevel = level;
                target.Collapsed = collapsed;
                levels = Math.Max(levels, level);
            }

            foreach (var (_, target) in view.Rows.Entries)
            {
                if (target.Cells.Any(cell => cell?.Formula != null && cell.Formula.Contains("SUBTOTAL(", StringComparison.OrdinalIgnoreCase)))
                    target.Kind = SheetRowKind.Total;
            }

            view.OutlineRowLevels = levels;
        }
    }
}
