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
            for (var row = 0; row < view.Rows.Count; row++)
            {
                var source = context.Source.FindRow(row);
                var target = view.Rows[row];
                target.OutlineLevel = (int)(source?.OutlineLevel?.Value ?? 0);
                target.Collapsed = source?.Collapsed?.Value == true;
                levels = Math.Max(levels, target.OutlineLevel);
                if (target.Cells.Any(cell => cell?.Formula != null && cell.Formula.Contains("SUBTOTAL(", StringComparison.OrdinalIgnoreCase)))
                    target.Kind = SheetRowKind.Total;
            }

            view.OutlineRowLevels = levels;
        }
    }
}
