using System.Collections.Generic;
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
            CellRange reference = null;
            IEnumerable<FilterColumn> columns = Enumerable.Empty<FilterColumn>();
            var auto = context.Source.Worksheet?.GetFirstChild<XlAutoFilter>();
            if (auto?.Reference?.Value != null && CellRange.TryParse(auto.Reference.Value, out var sheetRange))
            {
                reference = sheetRange;
                columns = auto.Elements<FilterColumn>();
            }
            else
            {
                foreach (var table in ExcelTables.On(context.Source))
                {
                    var tableFilter = table.Definition.AutoFilter;
                    if (tableFilter == null || !table.FilterButtons || !tableFilter.Elements<FilterColumn>().Any(HasCriteria))
                        continue;

                    reference = table.Range;
                    columns = tableFilter.Elements<FilterColumn>();
                    break;
                }
            }

            if (reference == null)
                return;

            var region = context.Clip(reference);
            if (region == null || region.Value.Bottom <= region.Value.Top)
                return;

            var area = region.Value;
            var filter = new SheetFilter { Range = area };
            foreach (var column in columns)
            {
                var index = area.Left + (int)(column.ColumnId?.Value ?? 0);
                var active = HasCriteria(column);
                var values = column.GetFirstChild<Filters>();
                var evaluable = active && values != null && !column.Elements<CustomFilters>().Any() && !column.Elements<Top10>().Any() && !column.Elements<DynamicFilter>().Any() && !values.Elements<DateGroupItem>().Any();
                filter.Columns.Add(new SheetFilterColumn
                {
                    Index = index,
                    Active = active,
                    Summary = active ? "조건" : null,
                    Values = values?.Elements<Filter>().Select(item => item.Val?.Value ?? string.Empty).ToList() ?? new List<string>(),
                    IncludeBlank = values?.Blank?.Value == true,
                    Evaluable = evaluable,
                });
            }

            for (var row = area.Top + 1; row <= area.Bottom; row++)
            {
                filter.TotalCount++;
                if (!context.View.IsRowHidden(row))
                    filter.MatchCount++;
            }

            context.View.Filter = filter;
        }

        private static bool HasCriteria(FilterColumn column) =>
            column.Elements<Filters>().Any()
            || column.Elements<CustomFilters>().Any()
            || column.Elements<Top10>().Any()
            || column.Elements<DynamicFilter>().Any();
    }
}
