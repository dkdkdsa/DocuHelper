using System.Collections.Generic;
using System.Threading.Tasks;
using DocuHelper.Editors;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter
    {
        private const string FlashFillPrompt = "고른 칸 옆에 내가 적어 둔 예시를 보고 같은 규칙을 찾아서, 아래 칸도 같은 방식으로 채워 줘. 규칙이 헷갈리면 먼저 물어봐 줘.";

        private void AddDataHandlers()
        {
            _runs.Add("getData", RunGetData);
            _runs.Add("refreshAll", RunRefresh);
            _runs.Add("sortAscending", _ => Sort(false));
            _runs.Add("sortDescending", _ => Sort(true));
            _runs.Add("validation", RunValidation);
            _runs.Add("whatIf", RunWhatIf);

            _tools.Add("filter", _ => Sheetwide(("action", State.IsFiltered ? "remove" : "apply"), ("range", State.Region)));
            _tools.Add("filterClear", _ => Sheetwide(("action", "clear_criteria")));
            _tools.Add("filterReapply", _ => Sheetwide(("action", "reapply")));
            _tools.Add("outlineGroup", arg => Outline(arg, "group"));
            _tools.Add("outlineUngroup", arg => Outline(arg, "ungroup"));

            _prompts.Add("flashFill", () => FlashFillPrompt);

            _pressed.Add("filtered", () => State.IsFiltered);

            _needs.Add("filterCriteriaToClear", _ => State.HasFilterCriteria ? null : "지울 필터 조건이 없어요.");
            _needs.Add("filterCriteriaToReapply", _ => State.HasFilterCriteria ? null : "다시 적용할 필터 조건이 없어요.");
            _needs.Add("outlineGroupable", _ => ToolHas("format_rows_columns", "group") ? null : PendingReason);

            _menus.Add("getData", _ => GetDataMenu());
            _menus.Add("refreshAll", _ => new[]
            {
                Entry("all", "모두 새로 고침", "수식을 모두 다시 계산해요"),
                RibbonMenus.Separator(),
                Entry("external", "연결 속성…", "Excel에서 열어서 해야 해요", blocked: ExternalBlocker()),
            });
            _menus.Add("validation", _ => ValidationMenu());
            _menus.Add("whatIf", _ => WhatIfMenu());
            _menus.Add("outlineGroup", _ => OutlineMenu("행 그룹", "열 그룹"));
            _menus.Add("outlineUngroup", _ => OutlineMenu("행 그룹 해제", "열 그룹 해제", true));
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
                DialogEntry("dialog", "데이터 유효성 검사…", ExcelDialogIds.Validation, null, "set_data_validation"),
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
                State.OpenDialog(ExcelDialogIds.Validation);
            else if (arg == "clear" && _surface.Prepare())
                Apply(EditCommand.Create("set_data_validation", Cells(("action", "clear"))));

            return Task.CompletedTask;
        }

        private IReadOnlyList<RibbonMenuItem> WhatIfMenu()
        {
            return new[]
            {
                Entry("scenario", "시나리오 관리자…", "Excel에서 열어서 해야 해요", blocked: ExternalBlocker()),
                DialogEntry("dialog", "목표값 찾기…", ExcelDialogIds.GoalSeek, "결과를 맞추려면 어떤 값이어야 하는지 찾아요", "goal_seek"),
                Entry("table", "데이터 표…", "Excel에서 열어서 해야 해요", blocked: ExternalBlocker()),
            };
        }

        private Task RunWhatIf(string arg)
        {
            if (arg == "dialog")
                State.OpenDialog(ExcelDialogIds.GoalSeek);
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
