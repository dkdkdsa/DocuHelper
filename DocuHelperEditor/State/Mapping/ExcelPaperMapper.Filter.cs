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
            var criteria = new List<(int Index, FilterColumn Column)>();
            foreach (var column in columns)
            {
                var index = area.Left + (int)(column.ColumnId?.Value ?? 0);
                var active = HasCriteria(column);
                var values = column.GetFirstChild<Filters>();
                var evaluable = active && values != null && !column.Elements<CustomFilters>().Any() && !column.Elements<Top10>().Any() && !column.Elements<DynamicFilter>().Any();
                filter.Columns.Add(new SheetFilterColumn
                {
                    Index = index,
                    Active = active,
                    Summary = active ? "조건" : null,
                    Values = values?.Elements<Filter>().Select(item => item.Val?.Value ?? string.Empty).ToList() ?? new List<string>(),
                    IncludeBlank = values?.Blank?.Value == true,
                    Evaluable = evaluable,
                });
                if (evaluable)
                    criteria.Add((index, column));
            }

            for (var row = area.Top + 1; row <= area.Bottom; row++)
            {
                filter.TotalCount++;
                if (criteria.All(item => Passes(context, row, item.Index, item.Column)))
                {
                    filter.MatchCount++;
                    continue;
                }

                context.View.Rows.Ensure(row).FilteredOut = true;
            }

            context.View.Filter = filter;
        }

        private static bool HasCriteria(FilterColumn column) =>
            column.Elements<Filters>().Any()
            || column.Elements<CustomFilters>().Any()
            || column.Elements<Top10>().Any()
            || column.Elements<DynamicFilter>().Any();

        private static bool Passes(ExcelFeatureContext context, int row, int index, FilterColumn column)
        {
            var values = column.GetFirstChild<Filters>();
            var text = context.Cell(row, index)?.Text;
            if (string.IsNullOrEmpty(text))
                return values.Blank?.Value == true;

            return values.Elements<Filter>().Any(item => item.Val?.Value == text);
        }
    }
}
