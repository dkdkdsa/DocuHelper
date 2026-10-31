using System.Collections.Generic;
using System.Threading.Tasks;
using DocuHelper.Editors;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter
    {
        private const string FlashFillPrompt = "고른 칸 옆에 내가 적어 둔 예시를 보고 같은 규칙을 찾아서, 아래 칸도 같은 방식으로 채워 줘. 규칙이 헷갈리면 먼저 물어봐 줘.";

        private void RegisterData()
        {
            AddFile("excel.data.getdata", arg => RunGetData(arg))
                .MenuFrom(GetDataMenu);
            AddDisabled("excel.data.fromtext", PendingReason);
            AddFile("excel.data.refreshall", arg => RunRefresh(arg))
                .Needs("set_workbook_options")
                .MenuFrom(() => new[]
                {
                    Entry("all", "모두 새로 고침", "수식을 모두 다시 계산해요"),
                    RibbonMenus.Separator(),
                    Entry("external", "연결 속성…", "Excel에서 열어서 해야 해요", blocked: ExternalBlocker()),
                });
            AddFile("excel.data.sort.asc", _ => Sort(false)).Needs("sort_range");
            AddFile("excel.data.sort.desc", _ => Sort(true)).Needs("sort_range");
            Dialog("excel.data.sort", "sort_range");
            Tool("excel.data.filter", "filter_range", _ => Sheetwide(("action", State.IsFiltered ? "remove" : "apply"), ("range", State.Region)))
                .PressedWhen(() => State.IsFiltered);
            Tool("excel.data.filter.clear", "filter_range", _ => Sheetwide(("action", "clear_criteria")))
                .EnabledWhen(() => State.HasFilterCriteria, "지울 필터 조건이 없어요.");
            Tool("excel.data.filter.reapply", "filter_range", _ => Sheetwide(("action", "reapply")))
                .EnabledWhen(() => State.HasFilterCriteria, "다시 적용할 필터 조건이 없어요.");
            Dialog("excel.data.texttocolumns", "split_text");
            AddAssistant("excel.data.flashfill", FlashFillPrompt);
            Dialog("excel.data.removeduplicates", "remove_duplicates");
            AddFile("excel.data.validation", arg => RunValidation(arg))
                .Needs("set_data_validation")
                .MenuFrom(ValidationMenu);
            AddFile("excel.data.whatif", arg => RunWhatIf(arg))
                .MenuFrom(WhatIfMenu);
            Group("excel.data.outline.menu",
                ("excel.data.group", "그룹"),
                ("excel.data.ungroup", "그룹 해제"),
                ("excel.data.subtotal", "부분합"));
            Tool("excel.data.group", "format_rows_columns", arg => Outline(arg, "group"))
                .EnabledWhen(() => ToolHas("format_rows_columns", "group"), PendingReason)
                .MenuFrom(() => OutlineMenu("행 그룹", "열 그룹"));
            Tool("excel.data.ungroup", "format_rows_columns", arg => Outline(arg, "ungroup"))
                .EnabledWhen(() => ToolHas("format_rows_columns", "group"), PendingReason)
                .MenuFrom(() => OutlineMenu("행 그룹 해제", "열 그룹 해제", true));
            Dialog("excel.data.subtotal", "add_subtotals");
        }

        private Task RunRefresh(string arg)
        {
            if (arg == "external")
                Workspace.OpenInOriginalApp(File.Id);
            else if (_surface.Prepare())
                Apply(EditCommand.Create("set_workbook_options", Workbook(("calculate_now", "workbook"))));

            return Task.CompletedTask;
        }

        private Task Sort(bool descending)
        {
            if (_surface.Prepare())
                _surface.SortTable(descending);

            return Task.CompletedTask;
        }

        private IReadOnlyList<RibbonMenuItem> GetDataMenu()
        {
            return new[]
            {
                Entry("text", "텍스트/CSV에서…", "CSV, TSV, 텍스트 파일", blocked: PendingReason),
                Entry("xlsx", "다른 엑셀 파일에서…", "작업 폴더의 엑셀 파일에서 시트나 범위를 가져와요", blocked: PendingReason),
                RibbonMenus.Separator(),
                Entry("external", "웹, 데이터베이스, 그 밖의 원본…", "Excel에서 열어서 해야 해요", blocked: ExternalBlocker()),
            };
        }

        private Task RunGetData(string arg)
        {
            if (arg == "external")
                Workspace.OpenInOriginalApp(File.Id);

            return Task.CompletedTask;
        }

        private IReadOnlyList<RibbonMenuItem> ValidationMenu()
        {
            return new[]
            {
                DialogEntry("dialog", "데이터 유효성 검사…", "excel.data.validation", null, "set_data_validation"),
                RibbonMenus.Separator(),
                Entry("circle", "잘못된 데이터 표시", "규칙에 맞지 않는 칸에 빨간 동그라미를 그려요", blocked: PendingReason),
                Entry("clear_circles", "유효성 표시 지우기", blocked: PendingReason),
                RibbonMenus.Separator(),
                Entry("clear", "유효성 검사 지우기"),
            };
        }

        private Task RunValidation(string arg)
        {
            if (arg == "dialog")
                State.OpenDialog("excel.data.validation");
            else if (arg == "clear" && _surface.Prepare())
                Apply(EditCommand.Create("set_data_validation", Cells(("action", "clear"))));

            return Task.CompletedTask;
        }

        private IReadOnlyList<RibbonMenuItem> WhatIfMenu()
        {
            return new[]
            {
                Entry("scenario", "시나리오 관리자…", "Excel에서 열어서 해야 해요", blocked: ExternalBlocker()),
                DialogEntry("dialog", "목표값 찾기…", "excel.data.whatif", "결과를 맞추려면 어떤 값이어야 하는지 찾아요", "goal_seek"),
                Entry("table", "데이터 표…", "Excel에서 열어서 해야 해요", blocked: ExternalBlocker()),
            };
        }

        private Task RunWhatIf(string arg)
        {
            if (arg == "dialog")
                State.OpenDialog("excel.data.whatif");
            else if (arg == "scenario" || arg == "table")
                Workspace.OpenInOriginalApp(File.Id);

            return Task.CompletedTask;
        }

        private IReadOnlyList<RibbonMenuItem> OutlineMenu(string rowsLabel, string columnsLabel, bool clear = false)
        {
            if (!clear && (_surface.WholeRows || _surface.WholeColumns))
                return null;

            var items = new List<RibbonMenuItem> { Entry("rows", rowsLabel), Entry("columns", columnsLabel) };
            if (clear)
            {
                items.Add(RibbonMenus.Separator());
                items.Add(Entry("clear", "윤곽 지우기", "시트의 모든 그룹을 풀어요"));
            }

            return items;
        }

        private Dictionary<string, object> Outline(string arg, string group)
        {
            var kind = arg;
            if (string.IsNullOrEmpty(kind))
                kind = _surface.WholeColumns && !_surface.WholeRows ? "columns" : "rows";

            return kind switch
            {
                "rows" => Sheetwide(("rows", State.Rows), ("group", group)),
                "columns" => Sheetwide(("columns", State.Columns), ("group", group)),
                "clear" => Sheetwide(("rows", "1:1048576"), ("group", "clear")),
                _ => null,
            };
        }
    }
}
