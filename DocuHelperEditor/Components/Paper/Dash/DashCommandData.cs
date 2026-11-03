using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashCommandData
    {
        public const string ModelNeed = "model";
        public const string SelectionNeed = "selection";
        public const string OneNeed = "one";
        public const string AnyFileNeed = "files";
        public const string ExcelFileNeed = "files:excel";
        public const string WordFileNeed = "files:word";
        public const string PptFileNeed = "files:ppt";
        public const string TableOriginNeed = "origin";


        private static readonly string[] s_fontNames = { "Gothic A1", "Hahmlet", "맑은 고딕", "굴림", "바탕" };

        private static readonly int[] s_fontSizes = { 9, 10, 11, 12, 13, 14, 16, 18, 20, 24, 28, 32, 36, 40, 48, 54, 60, 72 };

        private static readonly (string Label, string Hex)[] s_colors =
        {
            ("먹색", "1C2A48"), ("회색", "4A5570"), ("파랑", "2F5FA8"), ("빨강", "B5462A"),
            ("초록", "1E7145"), ("보라", "5B3FA0"), ("하양", "FFFFFF"),
        };

        private static readonly CommandMenuSpec[] s_shapes =
        {
            Entry("사각형", """{"type":"shape","shape":"rect"}"""),
            Entry("둥근 사각형", """{"type":"shape","shape":"round"}"""),
            Entry("타원", """{"type":"shape","shape":"ellipse"}"""),
            Entry("알약", """{"type":"shape","shape":"pill"}"""),
            Entry("선", """{"type":"line"}"""),
            Entry("화살표", """{"type":"line","arrowEnd":true}"""),
        };

        private static readonly CommandMenuSpec[] s_charts =
        {
            Entry("세로 막대", """{"type":"chart","kind":"column"}"""),
            Entry("가로 막대", """{"type":"chart","kind":"bar"}"""),
            Entry("꺾은선", """{"type":"chart","kind":"line"}"""),
            Entry("영역", """{"type":"chart","kind":"area"}"""),
            Entry("원형", """{"type":"chart","kind":"pie"}"""),
        };

        private static readonly CommandMenuSpec[] s_controls =
        {
            Entry("단추", """{"type":"button","label":"단추","action":{"type":"reset"}}"""),
            Entry("슬라이더", """{"type":"slider"}"""),
            Entry("기간 슬라이더", """{"type":"rangeSlider"}"""),
            Entry("스위치", """{"type":"toggle"}"""),
            Entry("단추 줄", """{"type":"segmented"}"""),
            Entry("펼침 목록", """{"type":"select"}"""),
        };

        private static readonly CommandMenuSpec[] s_fonts = s_fontNames
            .Select(name => Entry(name, "{\"fontName\":\"" + name + "\"}"))
            .ToArray();

        private static readonly CommandMenuSpec[] s_sizes = s_fontSizes
            .Select(size => Entry(size.ToString(CultureInfo.InvariantCulture), "{\"fontSize\":" + size.ToString(CultureInfo.InvariantCulture) + "}"))
            .ToArray();

        private static readonly CommandMenuSpec[] s_palette = s_colors
            .Select(color => new CommandMenuSpec { Label = color.Label, Swatch = color.Hex, Args = "{\"color\":\"#" + color.Hex + "\"}" })
            .ToArray();

        private static readonly CommandMenuSpec[] s_arrange =
        {
            Entry("맨 앞으로", """{"to":"front"}"""),
            Entry("앞으로", """{"to":"forward"}"""),
            Entry("뒤로", """{"to":"backward"}"""),
            Entry("맨 뒤로", """{"to":"back"}"""),
        };

        private static readonly CommandMenuSpec[] s_sources =
        {
            new() { Label = "엑셀 시트", Args = """{"type":"excel"}""", Need = ExcelFileNeed },
            new() { Label = "워드·한글 표", Args = """{"type":"word"}""", Need = WordFileNeed },
            new() { Label = "PPT 표", Args = """{"type":"pptTable"}""", Need = PptFileNeed },
            new() { Label = "PPT 차트", Args = """{"type":"pptChart"}""", Need = PptFileNeed },
        };

        private static readonly CommandMenuSpec[] s_params =
        {
            Entry("고르기", """{"type":"pick"}"""),
            Entry("범위", """{"type":"range"}"""),
            Entry("숫자", """{"type":"number"}"""),
            Entry("켜고 끄기", """{"type":"bool"}"""),
        };

        private static readonly CommandSpec[] s_data =
        {
            new() { Id = "dash.data.source", Class = RibbonCommandClass.Tool, Action = "source", Need = AnyFileNeed, Menu = s_sources },
            new() { Id = "dash.data.param", Class = RibbonCommandClass.Tool, Action = "param", Need = ModelNeed, Menu = s_params },
            new() { Id = "dash.data.table", Class = RibbonCommandClass.Tool, Action = "table", Need = TableOriginNeed },
            new() { Id = "dash.data.formula", Class = RibbonCommandClass.Tool, Action = "formula", Need = ModelNeed },
        };

        private static readonly CommandSpec[] s_home =
        {
            new() { Id = "dash.home.paste", Class = RibbonCommandClass.Clipboard, Action = "paste", Need = ModelNeed },
            new() { Id = "dash.home.cut", Class = RibbonCommandClass.Clipboard, Action = "cut", Need = SelectionNeed },
            new() { Id = "dash.home.copy", Class = RibbonCommandClass.Clipboard, Action = "copy", Need = SelectionNeed },
            new() { Id = "dash.home.fontName", Class = RibbonCommandClass.Tool, Action = "style", Need = SelectionNeed, Value = "fontName", Menu = s_fonts },
            new() { Id = "dash.home.fontSize", Class = RibbonCommandClass.Tool, Action = "style", Need = SelectionNeed, Value = "fontSize", Menu = s_sizes },
            new() { Id = "dash.home.fontGrow", Class = RibbonCommandClass.Tool, Action = "grow", Need = SelectionNeed },
            new() { Id = "dash.home.fontShrink", Class = RibbonCommandClass.Tool, Action = "shrink", Need = SelectionNeed },
            new() { Id = "dash.home.bold", Class = RibbonCommandClass.Tool, Action = "style", Need = SelectionNeed, Pressed = "bold", Args = """{"fontWeight":700}""", ArgsOff = """{"fontWeight":null}""" },
            new() { Id = "dash.home.italic", Class = RibbonCommandClass.Tool, Action = "style", Need = SelectionNeed, Pressed = "italic", Args = """{"italic":true}""", ArgsOff = """{"italic":null}""" },
            new() { Id = "dash.home.underline", Class = RibbonCommandClass.Tool, Action = "style", Need = SelectionNeed, Pressed = "underline", Args = """{"underline":true}""", ArgsOff = """{"underline":null}""" },
            new() { Id = "dash.home.fontColor", Class = RibbonCommandClass.Tool, Action = "style", Need = SelectionNeed, Color = "fontColor", Menu = s_palette },
            new() { Id = "dash.home.alignLeft", Class = RibbonCommandClass.Tool, Action = "style", Need = SelectionNeed, Pressed = "align:left", Args = """{"textAlign":"left"}""" },
            new() { Id = "dash.home.alignCenter", Class = RibbonCommandClass.Tool, Action = "style", Need = SelectionNeed, Pressed = "align:center", Args = """{"textAlign":"center"}""" },
            new() { Id = "dash.home.alignRight", Class = RibbonCommandClass.Tool, Action = "style", Need = SelectionNeed, Pressed = "align:right", Args = """{"textAlign":"right"}""" },
            new() { Id = "dash.home.mark", Class = RibbonCommandClass.Tool, Action = "style", Need = SelectionNeed, Pressed = "mark", Args = """{"mark":"#FFE35A"}""", ArgsOff = """{"mark":null}""" },
            new() { Id = "dash.home.shapes", Class = RibbonCommandClass.Tool, Action = "insert", Need = ModelNeed, Menu = s_shapes },
            new() { Id = "dash.home.arrange", Class = RibbonCommandClass.Tool, Action = "arrange", Need = SelectionNeed, Menu = s_arrange },
            new() { Id = "dash.home.settings", Class = RibbonCommandClass.Tool, Action = "settings", Need = OneNeed },
            new() { Id = "dash.home.delete", Class = RibbonCommandClass.Tool, Action = "delete", Need = SelectionNeed },
        };

        private static readonly CommandSpec[] s_insert =
        {
            new() { Id = "dash.insert.textbox", Class = RibbonCommandClass.Tool, Action = "insert", Need = ModelNeed, Args = """{"type":"text","text":"글을 입력하세요"}""" },
            new() { Id = "dash.insert.shapes", Class = RibbonCommandClass.Tool, Action = "insert", Need = ModelNeed, Menu = s_shapes },
            new() { Id = "dash.insert.chart", Class = RibbonCommandClass.Tool, Action = "insert", Need = ModelNeed, Menu = s_charts },
            new() { Id = "dash.insert.table", Class = RibbonCommandClass.Tool, Action = "insert", Need = ModelNeed, Args = """{"type":"table"}""" },
            new() { Id = "dash.insert.repeat", Class = RibbonCommandClass.Tool, Action = "insert", Need = ModelNeed, Args = """{"type":"repeat"}""" },
            new() { Id = "dash.insert.control", Class = RibbonCommandClass.Tool, Action = "insert", Need = ModelNeed, Menu = s_controls },
            new() { Id = "dash.insert.meter", Class = RibbonCommandClass.Tool, Action = "insert", Need = ModelNeed, Args = """{"type":"meter"}""" },
            new() { Id = "dash.insert.row", Class = RibbonCommandClass.Tool, Action = "insert", Need = ModelNeed, Args = """{"type":"row"}""" },
            new() { Id = "dash.insert.column", Class = RibbonCommandClass.Tool, Action = "insert", Need = ModelNeed, Args = """{"type":"column"}""" },
            new() { Id = "dash.insert.canvas", Class = RibbonCommandClass.Tool, Action = "insert", Need = ModelNeed, Args = """{"type":"canvas"}""" },
        };

        public static IReadOnlyList<CommandSpec> All { get; } = new[]
        {
            new CommandSpec { Id = DashRibbonCommands.UndoId, Class = RibbonCommandClass.File, Action = "undo" },
            new CommandSpec { Id = DashRibbonCommands.RedoId, Class = RibbonCommandClass.File, Action = "redo" },
            new CommandSpec { Id = "dash.file.save", Class = RibbonCommandClass.File, Action = "save" },
            new CommandSpec { Id = "dash.file.saveAs", Class = RibbonCommandClass.File, Action = "saveAs" },
            new CommandSpec { Id = "dash.file.info", Class = RibbonCommandClass.File, Action = "showInfo", WhileLocked = true },
            new CommandSpec { Id = "dash.file.pdf", Class = RibbonCommandClass.File, Action = "exportPdf", Need = ModelNeed },
            new CommandSpec { Id = "dash.file.print", Class = RibbonCommandClass.File, Action = "print", Need = ModelNeed },
            new CommandSpec { Id = "dash.view.show", Class = RibbonCommandClass.View, Action = "mode:" + DashViewState.ViewMode, Pressed = "mode:" + DashViewState.ViewMode },
            new CommandSpec { Id = "dash.view.edit", Class = RibbonCommandClass.View, Action = "mode:" + DashViewState.EditMode, Pressed = "mode:" + DashViewState.EditMode },
            new CommandSpec { Id = "dash.view.data", Class = RibbonCommandClass.View, Action = "mode:" + DashViewState.DataMode, Pressed = "mode:" + DashViewState.DataMode },
            new CommandSpec { Id = "dash.view.gridlines", Class = RibbonCommandClass.View, Action = "gridlines", Pressed = "gridlines" },
            new CommandSpec { Id = "dash.view.guides", Class = RibbonCommandClass.View, Action = "guides", Pressed = "guides" },
            new CommandSpec
            {
                Id = "dash.view.zoom",
                Class = RibbonCommandClass.View,
                Action = "zoom",
                Dialog = new DialogSpec("확대/축소", new[] { new DialogField("value", "배율", "choice", "100", new string[] { "200", "150", "100", "75", "66", "50", "fit" }, new string[] { "200", "150", "100", "75", "66", "50", "창에 맞춤" }) }),
            },
            new CommandSpec { Id = "dash.view.fit", Class = RibbonCommandClass.View, Action = "fit" },
            new CommandSpec { Id = "dash.data.refresh", Class = RibbonCommandClass.View, Action = "refresh" },
            new CommandSpec { Id = "dash.data.resetParams", Class = RibbonCommandClass.View, Action = "resetParams" },
        }.Concat(s_home).Concat(s_insert).Concat(s_data).ToList();

        private static CommandMenuSpec Entry(string label, string args) => new() { Label = label, Args = args };
    }
}
