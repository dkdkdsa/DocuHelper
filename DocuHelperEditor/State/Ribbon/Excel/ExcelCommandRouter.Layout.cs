using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelper.Tools;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter
    {
        private static readonly (string Value, string Label)[] s_themes =
        {
            ("office", "Office"),
            ("office2007", "Office 2007-2010"),
            ("grayscale", "회색 음영"),
            ("blue", "파랑"),
            ("green", "녹색"),
            ("orange", "주황"),
            ("red", "빨강"),
            ("violet", "보라"),
            ("yellow", "노랑"),
        };

        private static readonly (string Value, string Label)[] s_themeColors =
        {
            ("office", "Office"),
            ("office2007", "Office 2007-2010"),
            ("grayscale", "회색 음영"),
            ("blue", "파랑"),
            ("blue_warm", "파랑 따뜻함"),
            ("green", "녹색"),
            ("orange", "주황"),
            ("red", "빨강"),
            ("violet", "보라"),
            ("yellow", "노랑"),
        };

        private static readonly (string Value, string Label)[] s_themeFonts =
        {
            ("office", "Office (Calibri / 맑은 고딕)"),
            ("malgun", "맑은 고딕"),
            ("gulim", "굴림"),
            ("dotum", "돋움"),
            ("batang", "바탕"),
            ("arial", "Arial"),
            ("times", "Times New Roman"),
        };

        private static readonly (string Value, string Label, int Code)[] s_papers =
        {
            ("a3", "A3", 8),
            ("a4", "A4", 9),
            ("a5", "A5", 11),
            ("b4_jis", "B4 (JIS)", 12),
            ("b5_jis", "B5 (JIS)", 13),
            ("letter", "Letter", 1),
            ("legal", "Legal", 5),
            ("tabloid", "Tabloid", 3),
        };

        private static readonly int[] s_scales = { 200, 150, 120, 100, 90, 80, 70, 60, 50 };

        private void RegisterLayout()
        {
            WorkbookChoice("excel.layout.theme", "theme", s_themes);
            WorkbookChoice("excel.layout.themecolors", "theme_colors", s_themeColors);
            WorkbookChoice("excel.layout.themefonts", "theme_fonts", s_themeFonts);
            Tool("excel.layout.margins", "set_print_setup", arg => string.IsNullOrEmpty(arg) ? null : Sheetwide(("margins", arg)))
                .MenuFrom(MarginMenu);
            Tool("excel.layout.orientation", "set_print_setup", arg => string.IsNullOrEmpty(arg) ? null : Sheetwide(("orientation", arg)))
                .MenuFrom(() => new[]
                {
                    Entry("portrait", "세로", check: State.Sheet.Page.Orientation == SheetOrientation.Portrait),
                    Entry("landscape", "가로", check: State.Sheet.Page.Orientation == SheetOrientation.Landscape),
                });
            Tool("excel.layout.papersize", "set_print_setup", arg => string.IsNullOrEmpty(arg) ? null : Sheetwide(("paper_size", arg)))
                .MenuFrom(() => s_papers.Select(p => Entry(p.Value, p.Label, check: State.Sheet.Page.PaperSize == p.Code)).ToArray());
            Tool("excel.layout.printarea", "set_print_setup", arg => PrintArea(arg))
                .PressedWhen(() => !string.IsNullOrEmpty(State.Sheet.Page.PrintArea))
                .MenuFrom(() => new[]
                {
                    Entry("set", "인쇄 영역 설정"),
                    Entry("clear", "인쇄 영역 해제", blocked: string.IsNullOrEmpty(State.Sheet.Page.PrintArea) ? "정해 둔 인쇄 영역이 없어요." : null),
                    Entry("add", "인쇄 영역에 추가", blocked: string.IsNullOrEmpty(State.Sheet.Page.PrintArea) ? "먼저 인쇄 영역을 정해 주세요." : null),
                });
            Tool("excel.layout.breaks", "set_print_setup", arg => Breaks(arg))
                .MenuFrom(() => new[]
                {
                    Entry("insert", "페이지 나누기 삽입"),
                    Entry("remove", "페이지 나누기 제거"),
                    Entry("reset", "모든 페이지 나누기 해제"),
                });
            AddFile("excel.layout.background", _ => ToggleBackground())
                .Needs("set_print_setup")
                .PressedWhen(() => !string.IsNullOrEmpty(State.Sheet.Page.BackgroundUrl));
            Dialog("excel.layout.printtitles", "set_print_setup");
            Fit("excel.layout.fitwidth", "fit_width", () => State.Sheet.Page.FitWidth);
            Fit("excel.layout.fitheight", "fit_height", () => State.Sheet.Page.FitHeight);
            Tool("excel.layout.scale", "set_print_setup", arg => Sheetwide(("scale", ParseScale(arg))))
                .ValueFrom(() => (State.Sheet.Page.Scale ?? 100).ToString(CultureInfo.InvariantCulture) + "%")
                .EnabledWhen(() => !State.Sheet.Page.HasFitToPages, "너비와 높이를 자동으로 돌려야 배율을 바꿀 수 있어요.")
                .MenuFrom(ScaleMenu);
            SheetOption("excel.layout.gridlines.view", "set_sheet_view", "show_gridlines", () => State.Sheet.ShowGridlines);
            SheetOption("excel.layout.gridlines.print", "set_print_setup", "print_gridlines", () => State.Sheet.Page.GridlinesPrint);
            SheetOption("excel.layout.headings.view", "set_sheet_view", "show_headings", () => State.Sheet.ShowHeadings);
            SheetOption("excel.layout.headings.print", "set_print_setup", "print_headings", () => State.Sheet.Page.HeadingsPrint);
        }

        private void WorkbookChoice(string id, string key, (string Value, string Label)[] choices)
        {
            Tool(id, "set_workbook_options", arg => string.IsNullOrEmpty(arg) ? null : Workbook((key, arg)))
                .MenuFrom(() => choices.Select(c => Entry(c.Value, c.Label)).ToArray());
        }

        private void SheetOption(string id, string tool, string key, Func<bool> current)
        {
            Tool(id, tool, _ => Sheetwide((key, !current()))).PressedWhen(current);
        }

        private void Fit(string id, string key, Func<int?> current)
        {
            Tool(id, "set_print_setup", arg => Sheetwide((key, ParsePages(arg))))
                .ValueFrom(() => current() is int pages && pages > 0 ? pages.ToString(CultureInfo.InvariantCulture) + "페이지" : "자동")
                .MenuFrom(() =>
                {
                    var items = new List<RibbonMenuItem> { Entry("auto", "자동", check: !(current() > 0)) };
                    items.AddRange(Enumerable.Range(1, 9).Select(n => Entry(n.ToString(CultureInfo.InvariantCulture), n + "페이지", check: current() == n)));
                    return items;
                });
        }

        private static object ParsePages(string arg) => int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pages) ? pages : "auto";

        private static int ParseScale(string arg)
        {
            var text = (arg ?? string.Empty).Trim().TrimEnd('%').Trim();
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scale) || scale < 10 || scale > 400)
                throw new ToolInputException("배율은 10부터 400 사이의 숫자로 적어 주세요.");

            return scale;
        }

        private IReadOnlyList<RibbonMenuItem> ScaleMenu()
        {
            var current = State.Sheet.Page.Scale ?? 100;
            var items = s_scales.Select(s => Entry(s.ToString(CultureInfo.InvariantCulture), s + "%", check: s == current)).ToList();
            items.Add(RibbonMenus.Separator());
            items.Add(RibbonMenus.Input("직접 입력", current.ToString(CultureInfo.InvariantCulture), "10부터 400까지"));
            return items;
        }

        private string MarginPreset()
        {
            var page = State.Sheet.Page;
            bool Near(double value, double target) => Math.Abs(value - target) < 0.06;
            if (Near(page.MarginTop, 1.91) && Near(page.MarginBottom, 1.91) && Near(page.MarginLeft, 1.78) && Near(page.MarginRight, 1.78))
                return "normal";

            if (Near(page.MarginTop, 2.54) && Near(page.MarginBottom, 2.54) && Near(page.MarginLeft, 2.54) && Near(page.MarginRight, 2.54))
                return "wide";

            return Near(page.MarginTop, 1.91) && Near(page.MarginBottom, 1.91) && Near(page.MarginLeft, 0.64) && Near(page.MarginRight, 0.64) ? "narrow" : null;
        }

        private IReadOnlyList<RibbonMenuItem> MarginMenu()
        {
            var preset = MarginPreset();
            return new[]
            {
                Entry("normal", "보통", "위아래 1.9cm, 좌우 1.8cm", check: preset == "normal"),
                Entry("wide", "넓게", "모두 2.5cm", check: preset == "wide"),
                Entry("narrow", "좁게", "위아래 1.9cm, 좌우 0.6cm", check: preset == "narrow"),
                RibbonMenus.Separator(),
                DialogEntry("dialog", "사용자 지정 여백…", "excel.layout.margins", "위, 아래, 왼쪽, 오른쪽, 머리글, 바닥글", "set_print_setup"),
            };
        }

        private Dictionary<string, object> PrintArea(string arg)
        {
            var existing = State.Sheet.Page.PrintArea;
            return arg switch
            {
                "set" => Sheetwide(("print_area", State.Range)),
                "clear" => Sheetwide(("print_area", "none")),
                "add" => Sheetwide(("print_area", string.IsNullOrEmpty(existing) ? State.Range : existing + "," + State.Range)),
                _ => null,
            };
        }

        private Dictionary<string, object> Breaks(string arg)
        {
            return arg switch
            {
                "insert" => Sheetwide(("breaks", Workbook(("row_before", State.ActiveRow), ("column_before", State.ActiveColumn)))),
                "remove" => Sheetwide(("breaks", Workbook(("remove_at", State.ActiveRef)))),
                "reset" => Sheetwide(("breaks", Workbook(("reset", true)))),
                _ => null,
            };
        }

        private async Task ToggleBackground()
        {
            if (!_surface.Prepare())
                return;

            if (!string.IsNullOrEmpty(State.Sheet.Page.BackgroundUrl))
            {
                Apply(EditCommand.Create("set_print_setup", Sheetwide(("background", "none"))));
                return;
            }

            var path = await _dialogs.PickImageAsync();
            if (!string.IsNullOrEmpty(path))
                Apply(EditCommand.Create("set_print_setup", Sheetwide(("background", path))));
        }
    }
}
