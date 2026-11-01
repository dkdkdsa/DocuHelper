using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;
using XlAutoFilter = DocumentFormat.OpenXml.Spreadsheet.AutoFilter;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        static partial void MapFilter(ExcelFeatureContext context)
        {
            var auto = context.Source.Worksheet?.GetFirstChild<XlAutoFilter>();
            if (auto?.Reference?.Value == null || !CellRange.TryParse(auto.Reference.Value, out var range))
                return;

            var region = context.Clip(range);
            if (region == null || region.Value.Bottom <= region.Value.Top)
                return;

            var area = region.Value;
            var filter = new SheetFilter { Range = area };
            for (var row = area.Top + 1; row <= area.Bottom; row++)
            {
                filter.TotalCount++;
                if (!context.Source.IsRowHidden(row))
                {
                    filter.MatchCount++;
                    continue;
                }

                if (row < context.View.Rows.Count)
                    context.View.Rows[row].FilteredOut = true;
            }

            foreach (var column in auto.Elements<FilterColumn>())
            {
                var active = column.Elements<Filters>().Any()
                    || column.Elements<CustomFilters>().Any()
                    || column.Elements<Top10>().Any()
                    || column.Elements<DynamicFilter>().Any();
                filter.Columns.Add(new SheetFilterColumn
                {
                    Index = area.Left + (int)(column.ColumnId?.Value ?? 0),
                    Active = active,
                    Summary = active ? "조건" : null,
                });
            }

            context.View.Filter = filter;
        }
    }
}
