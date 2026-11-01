using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Editors;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter
    {
        private const string AssistantBusyText = "도우미가 일하는 중이에요. 끝나면 부탁할 수 있어요.";
        private const string NotOnDiskText = "저장된 파일이 없어서 열 수 없어요.";

        private static readonly (string Value, string Label)[] s_shapes =
        {
            ("line", "선"),
            ("rect", "사각형"),
            ("roundRect", "둥근 사각형"),
            ("ellipse", "타원"),
            ("triangle", "삼각형"),
            ("rtTriangle", "직각 삼각형"),
            ("diamond", "마름모"),
            ("pentagon", "오각형"),
            ("hexagon", "육각형"),
            ("rightArrow", "오른쪽 화살표"),
            ("leftArrow", "왼쪽 화살표"),
            ("upArrow", "위쪽 화살표"),
            ("downArrow", "아래쪽 화살표"),
            ("leftRightArrow", "양방향 화살표"),
            ("star5", "별 5개"),
            ("heart", "하트"),
            ("cloud", "구름"),
            ("wedgeRectCallout", "말풍선"),
            ("lightningBolt", "번개"),
        };

        private static readonly (string Value, string Label)[] s_columnCharts =
        {
            ("column:clustered", "묶은 세로 막대형"),
            ("column:stacked", "누적 세로 막대형"),
            ("column:percent_stacked", "100% 누적 세로 막대형"),
            ("bar:clustered", "묶은 가로 막대형"),
            ("bar:stacked", "누적 가로 막대형"),
            ("bar:percent_stacked", "100% 누적 가로 막대형"),
        };

        private static readonly (string Value, string Label)[] s_lineCharts =
        {
            ("line:clustered", "꺾은선형"),
            ("line:stacked", "누적 꺾은선형"),
            ("line:markers", "표식이 있는 꺾은선형"),
            ("area:clustered", "영역형"),
            ("scatter:markers", "분산형(XY)"),
        };

        private static readonly (string Value, string Label)[] s_pieCharts =
        {
            ("pie:", "원형"),
            ("doughnut:", "도넛형"),
        };

        private void RegisterInsert()
        {
            AddFile("excel.insert.pivot", arg => RunPivot(arg))
                .Needs("build_pivot_summary")
                .MenuFrom(PivotMenu);
            AddFile("excel.insert.table", _ => RunInsertTable())
                .Needs("manage_table")
                .PressedWhen(() => State.IsInTable)
                .EnabledWhen(() => !State.IsInTable, "이미 표 안에 있어요.");
            AddFile("excel.insert.picture", arg => InsertPicture(arg))
                .Needs("insert_drawing")
                .MenuFrom(() => new[]
                {
                    Entry("device", "이 디바이스에서…", "PNG, JPG, GIF, BMP"),
                    RibbonMenus.Separator(),
                    Entry("online", "온라인 그림…", "인터넷 서비스라서 쓸 수 없어요", blocked: "온라인 그림과 스톡 이미지는 인터넷에 연결된 엑셀에서만 쓸 수 있어요. 내 컴퓨터의 그림을 넣어 주세요."),
                });
            Tool("excel.insert.shapes", "insert_drawing", arg => string.IsNullOrEmpty(arg)
                    ? null
                    : Sheetwide(("kind", "shape"), ("anchor", State.ActiveRef), ("shape", Workbook(("geometry", arg)))))
                .MenuFrom(() => s_shapes.Select(s => Entry(s.Value, s.Label)).ToArray());
            AddDisabled("excel.insert.icons", "아이콘은 인터넷에서 가져오는 그림이라 쓸 수 없어요.");
            Dialog("excel.insert.chart.recommended", "create_chart");
            Chart("excel.insert.chart.column", s_columnCharts);
            Chart("excel.insert.chart.line", s_lineCharts);
            Chart("excel.insert.chart.pie", s_pieCharts);
            Dialog("excel.insert.link", "set_hyperlink").PressedWhen(() => State.Cell?.Hyperlink != null);
            Dialog("excel.insert.note", "edit_comment");
            Tool("excel.insert.textbox", "insert_drawing", _ => Sheetwide(("kind", "text_box"), ("anchor", State.ActiveRef), ("text", "텍스트를 입력하세요")));
            Dialog(ExcelDialogIds.HeaderFooter, "set_print_setup");
            AddExternal("excel.insert.equation")
                .MenuFrom(() => new[] { Entry("open", "Excel에서 열기", "수식은 Excel에서 넣을 수 있어요") });
            Dialog("excel.insert.symbol", "write_cells");
        }

        private void Chart(string id, (string Value, string Label)[] kinds)
        {
            Tool(id, "create_chart", arg =>
                {
                    if (string.IsNullOrEmpty(arg))
                        return null;

                    var parts = arg.Split(':');
                    var args = Sheetwide(("data_range", State.Region), ("type", parts[0]));
                    if (parts.Length > 1 && parts[1].Length > 0)
                        args["subtype"] = parts[1];

                    return args;
                })
                .MenuFrom(() => kinds.Select(k => Entry(k.Value, k.Label)).ToArray());
        }

        private IReadOnlyList<RibbonMenuItem> PivotMenu()
        {
            return new[]
            {
                DialogEntry("dialog", "표 또는 범위로 요약표 만들기…", "excel.insert.pivot", "행 기준으로 합계나 개수를 모아서 보여 줘요"),
                RibbonMenus.Separator(),
                Entry("assistant", "추천 요약표", "도우미가 표를 보고 알맞은 요약표를 만들어요", blocked: AssistantBlocker()),
                Entry("external", "외부 데이터 원본에서…", "Excel에서 열어서 해야 해요", blocked: ExternalBlocker()),
            };
        }

        private Task RunPivot(string arg)
        {
            switch (arg)
            {
                case "dialog":
                    State.OpenDialog("excel.insert.pivot");
                    return Task.CompletedTask;
                case "assistant":
                    return PrefillAsync("이 표를 보고 알맞은 요약표를 만들어 줘. 어떤 항목을 행으로 놓고 무엇을 합계 낼지 먼저 알려 주고, 내가 괜찮다고 하면 새 시트에 수식으로 만들어 줘.");
                case "external":
                    Workspace.OpenInOriginalApp(File.Id);
                    return Task.CompletedTask;
                default:
                    return Task.CompletedTask;
            }
        }

        private string AssistantBlocker()
        {
            if (Assistant.Status == AssistantStatus.Asking)
                return "도우미가 물어본 것에 먼저 답해 주세요.";

            return Assistant.IsBusy || Assistant.IsRunning ? AssistantBusyText : null;
        }

        private string ExternalBlocker() => File != null && !string.IsNullOrEmpty(File.Path) && System.IO.File.Exists(File.Path) ? null : NotOnDiskText;

        private Task RunInsertTable()
        {
            if (_surface.Prepare())
                CreateTable("TableStyleMedium2");

            return Task.CompletedTask;
        }

        private async Task InsertPicture(string arg)
        {
            if (arg != "device" || !_surface.Prepare())
                return;

            var path = await _dialogs.PickImageAsync();
            if (string.IsNullOrEmpty(path))
                return;

            Apply(EditCommand.Create("insert_drawing", Sheetwide(("kind", "picture"), ("anchor", State.ActiveRef), ("source", path))));
        }

    }
}
