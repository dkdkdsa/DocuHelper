using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Editors;
using DocuHelperEditor.State.Papers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace DocuHelperEditor.Components.Paper.Excel
{
    public partial class ExcelPaper
    {
        private const string BlankFilterLabel = "(빈 셀)";
        private const int FilterValueLimit = 5000;

        private FilterRequest _filterMenu;
        private Sheet _summarySheet;
        private Dictionary<int, OutlineGroup> _summaries = new();

        private sealed record FilterRequest(string SheetName, SheetRegion Region, int Row, int Column, string Header, double X, double Y, string AnchorId, IReadOnlyList<string> Values, IReadOnlyCollection<string> Checked, bool Active, bool Truncated);

        private RenderFragment<SheetRowContext> RowDecorations => context => builder =>
        {
            builder.OpenComponent<SheetRowDecorations>(0);
            builder.AddAttribute(1, nameof(SheetRowDecorations.Context), context);
            builder.AddAttribute(2, nameof(SheetRowDecorations.Summaries), Summaries);
            builder.AddAttribute(3, nameof(SheetRowDecorations.OnOutline), EventCallback.Factory.Create<string>(this, RunOutline));
            builder.CloseComponent();
        };

        private RenderFragment CornerDecorations => builder =>
        {
            builder.OpenComponent<SheetGutterCorner>(0);
            builder.AddAttribute(1, nameof(SheetGutterCorner.Levels), CurrentSheet.OutlineRowLevels);
            builder.AddAttribute(2, nameof(SheetGutterCorner.OnLevel), EventCallback.Factory.Create<int>(this, level => RunOutline("level:" + level.ToString(CultureInfo.InvariantCulture))));
            builder.CloseComponent();
        };

        private string FilterStatus
        {
            get
            {
                var filter = CurrentSheet.Filter;
                if (filter == null || !filter.IsActive)
                    return null;

                return "레코드 " + filter.TotalCount.ToString(CultureInfo.InvariantCulture) + "개 중 " + filter.MatchCount.ToString(CultureInfo.InvariantCulture) + "개를 찾았어요";
            }
        }

        private IReadOnlyDictionary<int, OutlineGroup> Summaries
        {
            get
            {
                if (!ReferenceEquals(_summarySheet, CurrentSheet))
                {
                    _summarySheet = CurrentSheet;
                    _summaries = SheetOutline.Groups(CurrentSheet).ToDictionary(group => group.Summary);
                }

                return _summaries;
            }
        }

        private void OpenFilterMenu(SheetMenuRequest request, string anchorId = null)
        {
            var sheet = CurrentSheet;
            var region = SheetFilterRules.RegionAt(sheet, request.Row, request.Column);
            if (region == null)
                return;

            var area = region.Value;
            var all = new HashSet<string>(StringComparer.Ordinal);
            var visible = new HashSet<string>(StringComparer.Ordinal);
            for (var row = area.Top + 1; row <= area.Bottom; row++)
            {
                var text = FilterText(sheet, row, request.Column);
                all.Add(text);
                if (!sheet.IsRowHidden(row))
                    visible.Add(text);
            }

            var values = all.OrderBy(value => value, StringComparer.CurrentCulture).ToList();
            var truncated = values.Count > FilterValueLimit;
            if (truncated)
                values = values.Take(FilterValueLimit).ToList();

            var column = sheet.Filter?.FindColumn(request.Column);
            var active = column?.Active == true;
            IReadOnlyCollection<string> checkedValues;
            if (!active)
                checkedValues = values;
            else if (column.Evaluable)
                checkedValues = values.Where(value => column.Values.Contains(value) || (value == BlankFilterLabel && column.IncludeBlank)).ToList();
            else
                checkedValues = values.Where(visible.Contains).ToList();

            _filterMenu = new FilterRequest(
                sheet.Name,
                area,
                request.Row,
                request.Column,
                SheetRef.ColumnName(request.Column) + " " + FilterText(sheet, area.Top, request.Column),
                request.X,
                request.Y,
                anchorId,
                values,
                checkedValues,
                active,
                truncated);
        }

        private void OpenFilterFromKeyboard()
        {
            if (IsLocked || (IsEditing && !CommitEdit()))
                return;

            SheetRef.TryParse(SelectedRef, out var column, out var row);
            OpenFilterMenu(new SheetMenuRequest("filter", row, column, 0, 0), SinkId);
        }

        private static string FilterText(Sheet sheet, int row, int column)
        {
            var text = sheet.FindCell(row, column)?.Text;
            return string.IsNullOrEmpty(text) ? BlankFilterLabel : text;
        }

        private void ApplyFilterMenu(List<string> selected)
        {
            var menu = _filterMenu;
            _filterMenu = null;
            if (menu == null)
                return;

            var range = menu.Region.ToRef();
            var column = SheetRef.ColumnName(menu.Column);
            if (selected == null)
                Edit(EditCommand.Create("filter_range", new { sheet = menu.SheetName, action = "clear_criteria", range, column }));
            else
                Edit(EditCommand.Create("filter_range", new { sheet = menu.SheetName, action = "set_criteria", range, column, values = selected.ToArray() }));

            _focusSink = true;
        }

        private void ClearFilterMenu()
        {
            var menu = _filterMenu;
            _filterMenu = null;
            if (menu != null)
                Edit(EditCommand.Create("filter_range", new { sheet = menu.SheetName, action = "clear_criteria", range = menu.Region.ToRef(), column = SheetRef.ColumnName(menu.Column) }));

            _focusSink = true;
        }

        private void SortFilterMenu(bool descending)
        {
            var menu = _filterMenu;
            _filterMenu = null;
            if (menu != null)
                Edit(EditCommand.Create("sort_range", new { sheet = menu.SheetName, range = menu.Region.ToRef(), by = SheetRef.ColumnName(menu.Column), descending, has_header = true }));

            _focusSink = true;
        }

        private void CloseFilterMenu()
        {
            _filterMenu = null;
            _focusSink = true;
        }

        private void RunOutline(string command)
        {
            if (IsLocked || (IsEditing && !CommitEdit()))
                return;

            var sheet = CurrentSheet.Name;
            var parts = command.Split(':');
            if (parts[0] == "level")
            {
                var level = int.Parse(parts[1], CultureInfo.InvariantCulture);
                var commands = SheetOutline.Groups(CurrentSheet)
                    .Select(group => EditCommand.Create("format_rows_columns", new { sheet, rows = RowsText(group.First, group.Last), outline = group.Level >= level ? "collapse" : "expand" }))
                    .ToList();
                if (commands.Count > 0)
                    Edits(commands);

                return;
            }

            var first = int.Parse(parts[1], CultureInfo.InvariantCulture);
            var last = int.Parse(parts[2], CultureInfo.InvariantCulture);
            Edit(EditCommand.Create("format_rows_columns", new { sheet, rows = RowsText(first, last), outline = parts[0] }));
        }

        private static string RowsText(int first, int last) => (first + 1).ToString(CultureInfo.InvariantCulture) + ":" + (last + 1).ToString(CultureInfo.InvariantCulture);
    }
}
