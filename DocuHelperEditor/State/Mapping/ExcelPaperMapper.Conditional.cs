using System;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        static partial void MapConditional(ExcelFeatureContext context)
        {
            var usedColumns = Math.Max(1, Math.Min(context.ColumnCount, (context.Source.UsedRange?.Right ?? 0) + 1));
            var rows = Math.Min(context.LastRow + 1, ExcelFeatureContext.CellBudget / usedColumns);
            if (rows <= 0 || context.ColumnCount <= 0)
                return;

            var evaluator = new ConditionalFormatEvaluator(context.Book, context.Source);
            if (!evaluator.HasRules)
                return;

            foreach (var pair in evaluator.Evaluate(new CellRange(0, 0, context.ColumnCount - 1, rows - 1)))
            {
                if (pair.Value.IsEmpty)
                    continue;

                var cell = context.EnsureCell(pair.Key.Row, pair.Key.Column);
                if (cell != null)
                    cell.Visual = ToVisual(pair.Value);
            }
        }

        private static SheetVisual ToVisual(CellVisual visual) => new()
        {
            FillColor = visual.Fill,
            FontColor = visual.FontColor,
            Bold = visual.Bold ? true : null,
            Italic = visual.Italic ? true : null,
            Underline = visual.Underline ? true : null,
            Strike = visual.Strike,
            Border = visual.Border,
            BarFraction = visual.BarFraction,
            BarColor = visual.BarColor,
            ScaleFill = visual.ScaleFill,
            IconSet = visual.HasIcon ? visual.IconSet : null,
            IconIndex = visual.HasIcon ? visual.IconIndex : null,
            IconCount = visual.HasIcon ? visual.IconCount : 0,
            HideValue = visual.HideValue,
        };
    }
}
