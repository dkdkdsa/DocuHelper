using System.Collections.Generic;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    internal static class PptCommandData
    {
        public static IReadOnlyList<CommandSpec> All { get; } = new[]
        {
            new CommandSpec
            {
                Id = "ppt.home.newSlide",
                Class = RibbonCommandClass.Tool,
                Tool = "add_slide",
                Args = """{"after":"{slide}","layout":"same_as_current"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "제목 슬라이드", Detail = "표준 레이아웃", Args = """{"layout":"title"}""" },
                    new CommandMenuSpec { Label = "제목 및 내용", Detail = "표준 레이아웃", Args = """{"layout":"title_content"}""" },
                    new CommandMenuSpec { Label = "구역 머리글", Detail = "표준 레이아웃", Args = """{"layout":"section"}""" },
                    new CommandMenuSpec { Label = "콘텐츠 2개", Detail = "표준 레이아웃", Args = """{"layout":"two_content"}""" },
                    new CommandMenuSpec { Label = "비교", Detail = "표준 레이아웃", Args = """{"layout":"comparison"}""" },
                    new CommandMenuSpec { Label = "제목만", Detail = "표준 레이아웃", Args = """{"layout":"title_only"}""" },
                    new CommandMenuSpec { Label = "빈 화면", Detail = "표준 레이아웃", Args = """{"layout":"blank"}""" },
                    new CommandMenuSpec { Label = "캡션 있는 콘텐츠", Detail = "표준 레이아웃", Args = """{"layout":"content_caption"}""" },
                    new CommandMenuSpec { Label = "캡션 있는 그림", Detail = "표준 레이아웃", Args = """{"layout":"picture_caption"}""" },
                    new CommandMenuSpec { Label = "선택한 슬라이드 복제", Detail = "슬라이드", Args = """{"action":"duplicate","slides":"{slides}"}""", Tool = "arrange_slides" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.home.layout",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_layout",
                Args = """{"slides":"{slides}","mode":"apply","layout":"{value}"}""",
                Need = "slide",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "제목 슬라이드", Detail = "표준 레이아웃", Args = """{"layout":"title"}""" },
                    new CommandMenuSpec { Label = "제목 및 내용", Detail = "표준 레이아웃", Args = """{"layout":"title_content"}""" },
                    new CommandMenuSpec { Label = "구역 머리글", Detail = "표준 레이아웃", Args = """{"layout":"section"}""" },
                    new CommandMenuSpec { Label = "콘텐츠 2개", Detail = "표준 레이아웃", Args = """{"layout":"two_content"}""" },
                    new CommandMenuSpec { Label = "비교", Detail = "표준 레이아웃", Args = """{"layout":"comparison"}""" },
                    new CommandMenuSpec { Label = "제목만", Detail = "표준 레이아웃", Args = """{"layout":"title_only"}""" },
                    new CommandMenuSpec { Label = "빈 화면", Detail = "표준 레이아웃", Args = """{"layout":"blank"}""" },
                    new CommandMenuSpec { Label = "캡션 있는 콘텐츠", Detail = "표준 레이아웃", Args = """{"layout":"content_caption"}""" },
                    new CommandMenuSpec { Label = "캡션 있는 그림", Detail = "표준 레이아웃", Args = """{"layout":"picture_caption"}""" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.home.reset",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_layout",
                Args = """{"slides":"{slides}","mode":"reset"}""",
                Need = "slide",
            },
            new CommandSpec
            {
                Id = "ppt.home.fontName",
                Class = RibbonCommandClass.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","name":"{value}"}""",
                Need = "text",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "제목 글꼴", Detail = "테마 글꼴", Args = """{"name":"heading"}""" },
                    new CommandMenuSpec { Label = "본문 글꼴", Detail = "테마 글꼴", Args = """{"name":"body"}""" },
                    new CommandMenuSpec { Label = "맑은 고딕", Detail = "글꼴", Args = """{"name":"맑은 고딕"}""" },
                    new CommandMenuSpec { Label = "굴림", Detail = "글꼴", Args = """{"name":"굴림"}""" },
                    new CommandMenuSpec { Label = "돋움", Detail = "글꼴", Args = """{"name":"돋움"}""" },
                    new CommandMenuSpec { Label = "바탕", Detail = "글꼴", Args = """{"name":"바탕"}""" },
                    new CommandMenuSpec { Label = "궁서", Detail = "글꼴", Args = """{"name":"궁서"}""" },
                    new CommandMenuSpec { Label = "함초롬돋움", Detail = "글꼴", Args = """{"name":"함초롬돋움"}""" },
                    new CommandMenuSpec { Label = "함초롬바탕", Detail = "글꼴", Args = """{"name":"함초롬바탕"}""" },
                    new CommandMenuSpec { Label = "나눔고딕", Detail = "글꼴", Args = """{"name":"나눔고딕"}""" },
                    new CommandMenuSpec { Label = "나눔명조", Detail = "글꼴", Args = """{"name":"나눔명조"}""" },
                    new CommandMenuSpec { Label = "Arial", Detail = "글꼴", Args = """{"name":"Arial"}""" },
                    new CommandMenuSpec { Label = "Calibri", Detail = "글꼴", Args = """{"name":"Calibri"}""" },
                    new CommandMenuSpec { Label = "Times New Roman", Detail = "글꼴", Args = """{"name":"Times New Roman"}""" },
                    new CommandMenuSpec { Label = "Segoe UI", Detail = "글꼴", Args = """{"name":"Segoe UI"}""" },
                    new CommandMenuSpec { Label = "Consolas", Detail = "글꼴", Args = """{"name":"Consolas"}""" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.home.fontSize",
                Class = RibbonCommandClass.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","size":"{value}"}""",
                Need = "text",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "8", Args = """{"size":8}""" },
                    new CommandMenuSpec { Label = "9", Args = """{"size":9}""" },
                    new CommandMenuSpec { Label = "10", Args = """{"size":10}""" },
                    new CommandMenuSpec { Label = "10.5", Args = """{"size":10.5}""" },
                    new CommandMenuSpec { Label = "11", Args = """{"size":11}""" },
                    new CommandMenuSpec { Label = "12", Args = """{"size":12}""" },
                    new CommandMenuSpec { Label = "14", Args = """{"size":14}""" },
                    new CommandMenuSpec { Label = "16", Args = """{"size":16}""" },
                    new CommandMenuSpec { Label = "18", Args = """{"size":18}""" },
                    new CommandMenuSpec { Label = "20", Args = """{"size":20}""" },
                    new CommandMenuSpec { Label = "24", Args = """{"size":24}""" },
                    new CommandMenuSpec { Label = "28", Args = """{"size":28}""" },
                    new CommandMenuSpec { Label = "32", Args = """{"size":32}""" },
                    new CommandMenuSpec { Label = "36", Args = """{"size":36}""" },
                    new CommandMenuSpec { Label = "40", Args = """{"size":40}""" },
                    new CommandMenuSpec { Label = "44", Args = """{"size":44}""" },
                    new CommandMenuSpec { Label = "48", Args = """{"size":48}""" },
                    new CommandMenuSpec { Label = "54", Args = """{"size":54}""" },
                    new CommandMenuSpec { Label = "60", Args = """{"size":60}""" },
                    new CommandMenuSpec { Label = "66", Args = """{"size":66}""" },
                    new CommandMenuSpec { Label = "72", Args = """{"size":72}""" },
                    new CommandMenuSpec { Label = "80", Args = """{"size":80}""" },
                    new CommandMenuSpec { Label = "88", Args = """{"size":88}""" },
                    new CommandMenuSpec { Label = "96", Args = """{"size":96}""" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.home.fontGrow",
                Class = RibbonCommandClass.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","size_step":1}""",
                Need = "text",
            },
            new CommandSpec
            {
                Id = "ppt.home.fontShrink",
                Class = RibbonCommandClass.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","size_step":-1}""",
                Need = "text",
            },
            new CommandSpec
            {
                Id = "ppt.home.bold",
                Class = RibbonCommandClass.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","bold":true}""",
                ArgsOff = """{"bold":false}""",
                Need = "text",
                Pressed = "bold",
            },
            new CommandSpec
            {
                Id = "ppt.home.italic",
                Class = RibbonCommandClass.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","italic":true}""",
                ArgsOff = """{"italic":false}""",
                Need = "text",
                Pressed = "italic",
            },
            new CommandSpec
            {
                Id = "ppt.home.underline",
                Class = RibbonCommandClass.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","underline":true}""",
                ArgsOff = """{"underline":false}""",
                Need = "text",
                Pressed = "underline",
            },
            new CommandSpec
            {
                Id = "ppt.home.strike",
                Class = RibbonCommandClass.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","strike":true}""",
                ArgsOff = """{"strike":false}""",
                Need = "text",
                Pressed = "strike",
            },
            new CommandSpec
            {
                Id = "ppt.home.fontColor",
                Class = RibbonCommandClass.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","color":"{color}"}""",
                Need = "text",
                Dialog = new DialogSpec("글꼴 색", new[] { new DialogField("color", "색", "color", "\"#B5462A\"", new string[] { "theme", "standard", "auto", "hex" }, new string[] { "테마 색 (10열 × 6행)", "표준 색", "자동", "다른 색 직접 고르기" }) }),
            },
            new CommandSpec
            {
                Id = "ppt.home.bullets",
                Class = RibbonCommandClass.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","bullet":"bullet"}""",
                ArgsOff = """{"bullet":"none"}""",
                Need = "text",
                Pressed = "bullet",
            },
            new CommandSpec
            {
                Id = "ppt.home.numbers",
                Class = RibbonCommandClass.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","bullet":"number","number_style":"arabic_period"}""",
                ArgsOff = """{"bullet":"none"}""",
                Need = "text",
                Pressed = "number",
            },
            new CommandSpec
            {
                Id = "ppt.home.outdent",
                Class = RibbonCommandClass.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","level_change":-1}""",
                Need = "text",
            },
            new CommandSpec
            {
                Id = "ppt.home.indent",
                Class = RibbonCommandClass.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","level_change":1}""",
                Need = "text",
            },
            new CommandSpec
            {
                Id = "ppt.home.lineSpacing",
                Class = RibbonCommandClass.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","line_spacing":"{line_spacing}","space_before":"{space_before}","space_after":"{space_after}"}""",
                Need = "text",
                Dialog = new DialogSpec("줄 간격", new[] { new DialogField("line_spacing", "줄 간격", "choice", "1.0", new string[] { "1.0", "1.15", "1.5", "2.0", "2.5", "3.0" }, new string[] { "1.0", "1.15", "1.5", "2.0", "2.5", "3.0" }), new DialogField("space_before", "단락 앞 간격(pt)", "number", "0", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("space_after", "단락 뒤 간격(pt)", "number", "0", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "ppt.home.alignLeft",
                Class = RibbonCommandClass.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","align":"left"}""",
                Need = "text",
                Pressed = "align:left",
            },
            new CommandSpec
            {
                Id = "ppt.home.alignCenter",
                Class = RibbonCommandClass.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","align":"center"}""",
                ArgsOff = """{"align":"left"}""",
                Need = "text",
                Pressed = "align:center",
            },
            new CommandSpec
            {
                Id = "ppt.home.alignRight",
                Class = RibbonCommandClass.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","align":"right"}""",
                ArgsOff = """{"align":"left"}""",
                Need = "text",
                Pressed = "align:right",
            },
            new CommandSpec
            {
                Id = "ppt.home.justify",
                Class = RibbonCommandClass.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","align":"justify"}""",
                ArgsOff = """{"align":"left"}""",
                Need = "text",
                Pressed = "align:justify",
            },
            new CommandSpec
            {
                Id = "ppt.home.shapes",
                Class = RibbonCommandClass.Tool,
                Tool = "add_slide_shape",
                Args = """{"slide":"{slide}","shape_type":"{value}"}""",
                Need = "slide",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "선", Detail = "선", Args = """{"shape_type":"line"}""" },
                    new CommandMenuSpec { Label = "화살표", Detail = "선", Args = """{"shape_type":"arrow_line"}""" },
                    new CommandMenuSpec { Label = "양방향 화살표", Detail = "선", Args = """{"shape_type":"double_arrow_line"}""" },
                    new CommandMenuSpec { Label = "꺾인 화살표", Detail = "선", Args = """{"shape_type":"elbow_arrow"}""" },
                    new CommandMenuSpec { Label = "곡선 화살표", Detail = "선", Args = """{"shape_type":"curved_arrow"}""" },
                    new CommandMenuSpec { Label = "텍스트 상자", Detail = "텍스트 상자", Args = """{"shape_type":"textbox"}""" },
                    new CommandMenuSpec { Label = "사각형", Detail = "사각형", Args = """{"shape_type":"rect"}""" },
                    new CommandMenuSpec { Label = "둥근 사각형", Detail = "사각형", Args = """{"shape_type":"round_rect"}""" },
                    new CommandMenuSpec { Label = "타원", Detail = "기본 도형", Args = """{"shape_type":"ellipse"}""" },
                    new CommandMenuSpec { Label = "이등변 삼각형", Detail = "기본 도형", Args = """{"shape_type":"triangle"}""" },
                    new CommandMenuSpec { Label = "직각 삼각형", Detail = "기본 도형", Args = """{"shape_type":"right_triangle"}""" },
                    new CommandMenuSpec { Label = "마름모", Detail = "기본 도형", Args = """{"shape_type":"diamond"}""" },
                    new CommandMenuSpec { Label = "오각형", Detail = "기본 도형", Args = """{"shape_type":"pentagon"}""" },
                    new CommandMenuSpec { Label = "육각형", Detail = "기본 도형", Args = """{"shape_type":"hexagon"}""" },
                    new CommandMenuSpec { Label = "평행 사변형", Detail = "기본 도형", Args = """{"shape_type":"parallelogram"}""" },
                    new CommandMenuSpec { Label = "사다리꼴", Detail = "기본 도형", Args = """{"shape_type":"trapezoid"}""" },
                    new CommandMenuSpec { Label = "십자형", Detail = "기본 도형", Args = """{"shape_type":"cross"}""" },
                    new CommandMenuSpec { Label = "도넛", Detail = "기본 도형", Args = """{"shape_type":"donut"}""" },
                    new CommandMenuSpec { Label = "하트", Detail = "기본 도형", Args = """{"shape_type":"heart"}""" },
                    new CommandMenuSpec { Label = "해", Detail = "기본 도형", Args = """{"shape_type":"sun"}""" },
                    new CommandMenuSpec { Label = "달", Detail = "기본 도형", Args = """{"shape_type":"moon"}""" },
                    new CommandMenuSpec { Label = "구름", Detail = "기본 도형", Args = """{"shape_type":"cloud"}""" },
                    new CommandMenuSpec { Label = "왼쪽 중괄호", Detail = "기본 도형", Args = """{"shape_type":"left_brace"}""" },
                    new CommandMenuSpec { Label = "오른쪽 중괄호", Detail = "기본 도형", Args = """{"shape_type":"right_brace"}""" },
                    new CommandMenuSpec { Label = "왼쪽 대괄호", Detail = "기본 도형", Args = """{"shape_type":"left_bracket"}""" },
                    new CommandMenuSpec { Label = "오른쪽 대괄호", Detail = "기본 도형", Args = """{"shape_type":"right_bracket"}""" },
                    new CommandMenuSpec { Label = "오른쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"right_arrow"}""" },
                    new CommandMenuSpec { Label = "왼쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"left_arrow"}""" },
                    new CommandMenuSpec { Label = "위쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"up_arrow"}""" },
                    new CommandMenuSpec { Label = "아래쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"down_arrow"}""" },
                    new CommandMenuSpec { Label = "좌우 화살표", Detail = "블록 화살표", Args = """{"shape_type":"left_right_arrow"}""" },
                    new CommandMenuSpec { Label = "갈매기형 수장", Detail = "블록 화살표", Args = """{"shape_type":"chevron"}""" },
                    new CommandMenuSpec { Label = "오각형 화살표", Detail = "블록 화살표", Args = """{"shape_type":"home_plate"}""" },
                    new CommandMenuSpec { Label = "순서도: 처리", Detail = "순서도", Args = """{"shape_type":"flow_process"}""" },
                    new CommandMenuSpec { Label = "순서도: 판단", Detail = "순서도", Args = """{"shape_type":"flow_decision"}""" },
                    new CommandMenuSpec { Label = "순서도: 터미널", Detail = "순서도", Args = """{"shape_type":"flow_terminator"}""" },
                    new CommandMenuSpec { Label = "순서도: 문서", Detail = "순서도", Args = """{"shape_type":"flow_document"}""" },
                    new CommandMenuSpec { Label = "별: 꼭짓점 5개", Detail = "별 및 현수막", Args = """{"shape_type":"star5"}""" },
                    new CommandMenuSpec { Label = "별: 꼭짓점 6개", Detail = "별 및 현수막", Args = """{"shape_type":"star6"}""" },
                    new CommandMenuSpec { Label = "말풍선: 사각형", Detail = "설명선", Args = """{"shape_type":"wedge_rect_callout"}""" },
                    new CommandMenuSpec { Label = "말풍선: 둥근 사각형", Detail = "설명선", Args = """{"shape_type":"wedge_round_callout"}""" },
                    new CommandMenuSpec { Label = "말풍선: 타원", Detail = "설명선", Args = """{"shape_type":"wedge_ellipse_callout"}""" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.home.arrange",
                Class = RibbonCommandClass.Tool,
                Tool = "arrange_slide_shapes",
                Args = """{"slide":"{slide}","shapes":"{shapes}"}""",
                Need = "shape",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "맨 앞으로 가져오기", Detail = "순서", Args = """{"action":"order","order":"front"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "앞으로 가져오기", Detail = "순서", Args = """{"action":"order","order":"forward"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "맨 뒤로 보내기", Detail = "순서", Args = """{"action":"order","order":"back"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "뒤로 보내기", Detail = "순서", Args = """{"action":"order","order":"backward"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "왼쪽 맞춤", Detail = "맞춤", Args = """{"action":"align","align":"left"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "가운데 맞춤", Detail = "맞춤", Args = """{"action":"align","align":"center"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "오른쪽 맞춤", Detail = "맞춤", Args = """{"action":"align","align":"right"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "위쪽 맞춤", Detail = "맞춤", Args = """{"action":"align","align":"top"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "중간 맞춤", Detail = "맞춤", Args = """{"action":"align","align":"middle"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "아래쪽 맞춤", Detail = "맞춤", Args = """{"action":"align","align":"bottom"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "가로 간격을 동일하게", Detail = "간격", Args = """{"action":"distribute","distribute":"horizontal"}""", Need = "shapes3" },
                    new CommandMenuSpec { Label = "세로 간격을 동일하게", Detail = "간격", Args = """{"action":"distribute","distribute":"vertical"}""", Need = "shapes3" },
                    new CommandMenuSpec { Label = "그룹", Detail = "그룹", Args = """{"action":"group"}""", Need = "shapes2" },
                    new CommandMenuSpec { Label = "그룹 해제", Detail = "그룹", Args = """{"action":"ungroup"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "오른쪽으로 90° 회전", Detail = "회전", Args = """{"action":"rotate","rotate":"cw90"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "왼쪽으로 90° 회전", Detail = "회전", Args = """{"action":"rotate","rotate":"ccw90"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "상하 대칭", Detail = "회전", Args = """{"action":"flip","flip":"vertical"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "좌우 대칭", Detail = "회전", Args = """{"action":"flip","flip":"horizontal"}""", Need = "shape" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.insert.newSlide",
                Class = RibbonCommandClass.Tool,
                Tool = "add_slide",
                Args = """{"after":"{slide}","layout":"same_as_current"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "제목 슬라이드", Detail = "표준 레이아웃", Args = """{"layout":"title"}""" },
                    new CommandMenuSpec { Label = "제목 및 내용", Detail = "표준 레이아웃", Args = """{"layout":"title_content"}""" },
                    new CommandMenuSpec { Label = "구역 머리글", Detail = "표준 레이아웃", Args = """{"layout":"section"}""" },
                    new CommandMenuSpec { Label = "콘텐츠 2개", Detail = "표준 레이아웃", Args = """{"layout":"two_content"}""" },
                    new CommandMenuSpec { Label = "비교", Detail = "표준 레이아웃", Args = """{"layout":"comparison"}""" },
                    new CommandMenuSpec { Label = "제목만", Detail = "표준 레이아웃", Args = """{"layout":"title_only"}""" },
                    new CommandMenuSpec { Label = "빈 화면", Detail = "표준 레이아웃", Args = """{"layout":"blank"}""" },
                    new CommandMenuSpec { Label = "캡션 있는 콘텐츠", Detail = "표준 레이아웃", Args = """{"layout":"content_caption"}""" },
                    new CommandMenuSpec { Label = "캡션 있는 그림", Detail = "표준 레이아웃", Args = """{"layout":"picture_caption"}""" },
                    new CommandMenuSpec { Label = "선택한 슬라이드 복제", Detail = "슬라이드", Args = """{"action":"duplicate","slides":"{slides}"}""", Tool = "arrange_slides" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.insert.table",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_slide_table",
                Args = """{"slide":"{slide}","action":"create","rows":"{rows}","columns":"{columns}","header_row":true,"banded_rows":true,"style":"medium_accent1"}""",
                Need = "slide",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "표 삽입…", Detail = "창", Dialog = new DialogSpec("표 넣기", new[] { new DialogField("columns", "열 개수", "number", "5", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("rows", "행 개수", "number", "4", System.Array.Empty<string>(), System.Array.Empty<string>()) }) },
                    new CommandMenuSpec { Label = "엑셀 표 가져오기", Detail = "도우미", Class = RibbonCommandClass.Assistant, Prompt = "{slideText} 엑셀 파일의 표를 가져와서 이 슬라이드에 표로 넣어 줘. 어느 범위를 넣을지 모르겠으면 먼저 물어봐 줘." },
                },
            },
            new CommandSpec
            {
                Id = "ppt.insert.picture",
                Class = RibbonCommandClass.Tool,
                Tool = "add_slide_picture",
                Args = """{"slide":"{slide}","image_base64":"{image}","name":"{imageName}"}""",
                Need = "slide",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "이 장치…", Detail = "파일 고르기", Picker = """{"title":"그림 고르기","extensions":[".png",".jpg",".jpeg",".gif",".bmp",".tif",".tiff"],"maxMegabytes":20}""" },
                    new CommandMenuSpec { Label = "클립보드의 그림", Detail = "붙여넣기", Args = """{"image_base64":"{clipImage}","name":"clipboard.png"}""", Need = "clipboardImage" },
                    new CommandMenuSpec { Label = "그림 바꾸기", Detail = "선택한 그림", Args = """{"replace":"{shape}"}""", Picker = """{"title":"바꿀 그림 고르기","extensions":[".png",".jpg",".jpeg",".gif",".bmp",".tif",".tiff"],"maxMegabytes":20}""", Need = "shape" },
                    new CommandMenuSpec { Label = "스톡 이미지", Detail = "온라인", Class = RibbonCommandClass.Disabled, Reason = "인터넷 그림 검색은 지원하지 않아요. 내 컴퓨터의 그림 파일을 골라 주세요." },
                    new CommandMenuSpec { Label = "온라인 그림", Detail = "온라인", Class = RibbonCommandClass.Disabled, Reason = "인터넷 그림 검색은 지원하지 않아요. 내 컴퓨터의 그림 파일을 골라 주세요." },
                },
            },
            new CommandSpec
            {
                Id = "ppt.insert.shapes",
                Class = RibbonCommandClass.Tool,
                Tool = "add_slide_shape",
                Args = """{"slide":"{slide}","shape_type":"{value}"}""",
                Need = "slide",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "선", Detail = "선", Args = """{"shape_type":"line"}""" },
                    new CommandMenuSpec { Label = "화살표", Detail = "선", Args = """{"shape_type":"arrow_line"}""" },
                    new CommandMenuSpec { Label = "양방향 화살표", Detail = "선", Args = """{"shape_type":"double_arrow_line"}""" },
                    new CommandMenuSpec { Label = "꺾인 화살표", Detail = "선", Args = """{"shape_type":"elbow_arrow"}""" },
                    new CommandMenuSpec { Label = "곡선 화살표", Detail = "선", Args = """{"shape_type":"curved_arrow"}""" },
                    new CommandMenuSpec { Label = "텍스트 상자", Detail = "텍스트 상자", Args = """{"shape_type":"textbox"}""" },
                    new CommandMenuSpec { Label = "사각형", Detail = "사각형", Args = """{"shape_type":"rect"}""" },
                    new CommandMenuSpec { Label = "둥근 사각형", Detail = "사각형", Args = """{"shape_type":"round_rect"}""" },
                    new CommandMenuSpec { Label = "타원", Detail = "기본 도형", Args = """{"shape_type":"ellipse"}""" },
                    new CommandMenuSpec { Label = "이등변 삼각형", Detail = "기본 도형", Args = """{"shape_type":"triangle"}""" },
                    new CommandMenuSpec { Label = "직각 삼각형", Detail = "기본 도형", Args = """{"shape_type":"right_triangle"}""" },
                    new CommandMenuSpec { Label = "마름모", Detail = "기본 도형", Args = """{"shape_type":"diamond"}""" },
                    new CommandMenuSpec { Label = "오각형", Detail = "기본 도형", Args = """{"shape_type":"pentagon"}""" },
                    new CommandMenuSpec { Label = "육각형", Detail = "기본 도형", Args = """{"shape_type":"hexagon"}""" },
                    new CommandMenuSpec { Label = "평행 사변형", Detail = "기본 도형", Args = """{"shape_type":"parallelogram"}""" },
                    new CommandMenuSpec { Label = "사다리꼴", Detail = "기본 도형", Args = """{"shape_type":"trapezoid"}""" },
                    new CommandMenuSpec { Label = "십자형", Detail = "기본 도형", Args = """{"shape_type":"cross"}""" },
                    new CommandMenuSpec { Label = "도넛", Detail = "기본 도형", Args = """{"shape_type":"donut"}""" },
                    new CommandMenuSpec { Label = "하트", Detail = "기본 도형", Args = """{"shape_type":"heart"}""" },
                    new CommandMenuSpec { Label = "해", Detail = "기본 도형", Args = """{"shape_type":"sun"}""" },
                    new CommandMenuSpec { Label = "달", Detail = "기본 도형", Args = """{"shape_type":"moon"}""" },
                    new CommandMenuSpec { Label = "구름", Detail = "기본 도형", Args = """{"shape_type":"cloud"}""" },
                    new CommandMenuSpec { Label = "왼쪽 중괄호", Detail = "기본 도형", Args = """{"shape_type":"left_brace"}""" },
                    new CommandMenuSpec { Label = "오른쪽 중괄호", Detail = "기본 도형", Args = """{"shape_type":"right_brace"}""" },
                    new CommandMenuSpec { Label = "왼쪽 대괄호", Detail = "기본 도형", Args = """{"shape_type":"left_bracket"}""" },
                    new CommandMenuSpec { Label = "오른쪽 대괄호", Detail = "기본 도형", Args = """{"shape_type":"right_bracket"}""" },
                    new CommandMenuSpec { Label = "오른쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"right_arrow"}""" },
                    new CommandMenuSpec { Label = "왼쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"left_arrow"}""" },
                    new CommandMenuSpec { Label = "위쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"up_arrow"}""" },
                    new CommandMenuSpec { Label = "아래쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"down_arrow"}""" },
                    new CommandMenuSpec { Label = "좌우 화살표", Detail = "블록 화살표", Args = """{"shape_type":"left_right_arrow"}""" },
                    new CommandMenuSpec { Label = "갈매기형 수장", Detail = "블록 화살표", Args = """{"shape_type":"chevron"}""" },
                    new CommandMenuSpec { Label = "오각형 화살표", Detail = "블록 화살표", Args = """{"shape_type":"home_plate"}""" },
                    new CommandMenuSpec { Label = "순서도: 처리", Detail = "순서도", Args = """{"shape_type":"flow_process"}""" },
                    new CommandMenuSpec { Label = "순서도: 판단", Detail = "순서도", Args = """{"shape_type":"flow_decision"}""" },
                    new CommandMenuSpec { Label = "순서도: 터미널", Detail = "순서도", Args = """{"shape_type":"flow_terminator"}""" },
                    new CommandMenuSpec { Label = "순서도: 문서", Detail = "순서도", Args = """{"shape_type":"flow_document"}""" },
                    new CommandMenuSpec { Label = "별: 꼭짓점 5개", Detail = "별 및 현수막", Args = """{"shape_type":"star5"}""" },
                    new CommandMenuSpec { Label = "별: 꼭짓점 6개", Detail = "별 및 현수막", Args = """{"shape_type":"star6"}""" },
                    new CommandMenuSpec { Label = "말풍선: 사각형", Detail = "설명선", Args = """{"shape_type":"wedge_rect_callout"}""" },
                    new CommandMenuSpec { Label = "말풍선: 둥근 사각형", Detail = "설명선", Args = """{"shape_type":"wedge_round_callout"}""" },
                    new CommandMenuSpec { Label = "말풍선: 타원", Detail = "설명선", Args = """{"shape_type":"wedge_ellipse_callout"}""" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.insert.smartart",
                Class = RibbonCommandClass.Tool,
                Tool = "add_slide_diagram",
                Args = """{"slide":"{slide}","kind":"{kind}","items":"{items}"}""",
                Need = "slide",
                Dialog = new DialogSpec("SmartArt 넣기", new[] { new DialogField("kind", "종류", "choice", "\"process\"", new string[] { "list", "process", "cycle", "hierarchy", "pyramid", "matrix" }, new string[] { "목록", "프로세스", "순환", "계층 구조", "피라미드", "행렬" }), new DialogField("items", "항목(한 줄에 하나)", "text", "\"첫째\\n둘째\\n셋째\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "ppt.insert.chart",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_slide_chart",
                Args = """{"slide":"{slide}","action":"create","chart_type":"{chart_type}","title":"{title}","categories":"{categories}","series":"{series}","legend":"{legend}","data_labels":"{data_labels}"}""",
                Need = "slide",
                Dialog = new DialogSpec("차트 넣기", new[] { new DialogField("chart_type", "종류", "choice", "\"column\"", new string[] { "column", "stacked_column", "bar", "stacked_bar", "line", "pie", "doughnut", "area" }, new string[] { "세로 막대", "누적 세로 막대", "가로 막대", "누적 가로 막대", "꺾은선", "원형", "도넛형", "영역형" }), new DialogField("title", "차트 제목", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("data", "데이터(엑셀에서 복사해 붙여넣기)", "text", "\"\\t매출\\n1월\\t3820\\n2월\\t3610\\n3월\\t4150\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("legend", "범례", "choice", "\"bottom\"", new string[] { "none", "bottom", "right", "top", "left" }, new string[] { "없음", "아래", "오른쪽", "위", "왼쪽" }), new DialogField("data_labels", "값 표시", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "ppt.insert.link",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_link",
                Args = """{"slide":"{slide}","shape":"{shape}","range":"{range}"}""",
                Need = "shape",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "웹 주소…", Detail = "링크", Args = """{"target":{"type":"url","url":"{url}","tooltip":"{tooltip}"}}""", Dialog = new DialogSpec("웹 주소 연결", new[] { new DialogField("url", "웹 주소", "text", "\"https://\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("tooltip", "설명 말풍선", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }) },
                    new CommandMenuSpec { Label = "이 문서의 슬라이드…", Detail = "링크", Args = """{"target":{"type":"slide","slide":"{slideNumber}"}}""", Dialog = new DialogSpec("슬라이드 연결", new[] { new DialogField("slideNumber", "슬라이드 번호", "number", "\"1\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }) },
                    new CommandMenuSpec { Label = "이전 슬라이드", Detail = "링크", Args = """{"target":{"type":"previous_slide"}}""" },
                    new CommandMenuSpec { Label = "다음 슬라이드", Detail = "링크", Args = """{"target":{"type":"next_slide"}}""" },
                    new CommandMenuSpec { Label = "처음 슬라이드", Detail = "링크", Args = """{"target":{"type":"first_slide"}}""" },
                    new CommandMenuSpec { Label = "마지막 슬라이드", Detail = "링크", Args = """{"target":{"type":"last_slide"}}""" },
                    new CommandMenuSpec { Label = "전자 메일…", Detail = "링크", Args = """{"target":{"type":"email","address":"{address}","subject":"{subject}"}}""", Dialog = new DialogSpec("전자 메일 연결", new[] { new DialogField("address", "받는 사람", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("subject", "제목", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }) },
                    new CommandMenuSpec { Label = "링크 지우기", Detail = "링크", Args = """{"target":{"type":"none"}}""", Need = "shape" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.insert.comment",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_slide_comment",
                Args = """{"slide":"{slide}","action":"add","text":"{text}","author":"{userName}","shape":"{shape}"}""",
                Need = "slide",
                Dialog = new DialogSpec("새 메모", new[] { new DialogField("text", "메모", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "ppt.insert.textbox",
                Class = RibbonCommandClass.Tool,
                Tool = "add_slide_shape",
                Args = """{"slide":"{slide}","shape_type":"textbox"}""",
                Need = "slide",
            },
            new CommandSpec
            {
                Id = "ppt.insert.headerFooter",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_footer",
                Args = """{"slides":"{scopeSlides}","footer":{"show":"{footer_show}","text":"{footer_text}"},"slide_number":"{slide_number}","date":{"show":"{date_show}","mode":"{date_mode}","format":"{date_format}","text":"{date_text}"},"skip_title_slide":"{skip_title_slide}"}""",
                Need = "slide",
                Dialog = new DialogSpec("머리글/바닥글", new[] { new DialogField("date_show", "날짜 및 시간", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("date_mode", "날짜 방식", "choice", "\"auto\"", new string[] { "auto", "fixed" }, new string[] { "자동으로 오늘 날짜", "직접 쓰기" }), new DialogField("date_format", "날짜 모양", "choice", "\"short\"", new string[] { "short", "long", "time" }, new string[] { "2026-10-31", "2026년 10월 31일 토요일", "오후 4:30" }), new DialogField("date_text", "직접 쓸 날짜", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("slide_number", "슬라이드 번호", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("footer_show", "바닥글", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("footer_text", "바닥글 글", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("skip_title_slide", "제목 슬라이드에는 보이지 않기", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("scope", "적용 범위", "choice", "\"slide\"", new string[] { "slide", "all" }, new string[] { "이 슬라이드만", "모든 슬라이드" }) }),
            },
            new CommandSpec
            {
                Id = "ppt.insert.slideNumber",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_footer",
                Args = """{"slides":"{scopeSlides}","footer":{"show":"{footer_show}","text":"{footer_text}"},"slide_number":"{slide_number}","date":{"show":"{date_show}","mode":"{date_mode}","format":"{date_format}","text":"{date_text}"},"skip_title_slide":"{skip_title_slide}"}""",
                Need = "slide",
                Dialog = new DialogSpec("머리글/바닥글", new[] { new DialogField("date_show", "날짜 및 시간", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("date_mode", "날짜 방식", "choice", "\"auto\"", new string[] { "auto", "fixed" }, new string[] { "자동으로 오늘 날짜", "직접 쓰기" }), new DialogField("date_format", "날짜 모양", "choice", "\"short\"", new string[] { "short", "long", "time" }, new string[] { "2026-10-31", "2026년 10월 31일 토요일", "오후 4:30" }), new DialogField("date_text", "직접 쓸 날짜", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("slide_number", "슬라이드 번호", "bool", "true", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("footer_show", "바닥글", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("footer_text", "바닥글 글", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("skip_title_slide", "제목 슬라이드에는 보이지 않기", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("scope", "적용 범위", "choice", "\"slide\"", new string[] { "slide", "all" }, new string[] { "이 슬라이드만", "모든 슬라이드" }) }),
            },
            new CommandSpec
            {
                Id = "ppt.insert.dateTime",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_footer",
                Args = """{"slides":"{scopeSlides}","footer":{"show":"{footer_show}","text":"{footer_text}"},"slide_number":"{slide_number}","date":{"show":"{date_show}","mode":"{date_mode}","format":"{date_format}","text":"{date_text}"},"skip_title_slide":"{skip_title_slide}"}""",
                Need = "slide",
                Dialog = new DialogSpec("머리글/바닥글", new[] { new DialogField("date_show", "날짜 및 시간", "bool", "true", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("date_mode", "날짜 방식", "choice", "\"auto\"", new string[] { "auto", "fixed" }, new string[] { "자동으로 오늘 날짜", "직접 쓰기" }), new DialogField("date_format", "날짜 모양", "choice", "\"short\"", new string[] { "short", "long", "time" }, new string[] { "2026-10-31", "2026년 10월 31일 토요일", "오후 4:30" }), new DialogField("date_text", "직접 쓸 날짜", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("slide_number", "슬라이드 번호", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("footer_show", "바닥글", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("footer_text", "바닥글 글", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("skip_title_slide", "제목 슬라이드에는 보이지 않기", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("scope", "적용 범위", "choice", "\"slide\"", new string[] { "slide", "all" }, new string[] { "이 슬라이드만", "모든 슬라이드" }) }),
            },
            new CommandSpec
            {
                Id = "ppt.insert.equation",
                Class = RibbonCommandClass.Assistant,
                Prompt = "{slideText}에 수식을 글상자로 넣어 줘. 식은 이렇게: ",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "분수", Detail = "수식", Prompt = "{slideText}에 분수 모양 수식을 글상자로 넣어 줘. 수식 내용은 이렇게: " },
                    new CommandMenuSpec { Label = "위첨자 / 아래첨자", Detail = "수식", Prompt = "{slideText}에 위첨자나 아래첨자가 들어간 식을 글상자로 넣어 줘. 식은 이렇게: " },
                    new CommandMenuSpec { Label = "근호", Detail = "수식", Prompt = "{slideText}에 제곱근 모양 식을 글상자로 넣어 줘. 식은 이렇게: " },
                    new CommandMenuSpec { Label = "합계 / 적분", Detail = "수식", Prompt = "{slideText}에 합계(Σ)나 적분(∫) 식을 글상자로 넣어 줘. 식은 이렇게: " },
                    new CommandMenuSpec { Label = "새 수식", Detail = "수식", Prompt = "{slideText}에 수식을 넣어 줘. 식은 이렇게: " },
                },
            },
            new CommandSpec
            {
                Id = "ppt.design.theme",
                Class = RibbonCommandClass.Tool,
                Tool = "set_deck_theme",
                Args = """{"theme":"{value}"}""",
                Need = "slide",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "Office 테마", Detail = "테마", Args = """{"theme":"office"}""", Swatch = "4472C4" },
                    new CommandMenuSpec { Label = "문서 도우미 남색", Detail = "테마", Args = """{"theme":"ink"}""", Swatch = "1C2A48" },
                    new CommandMenuSpec { Label = "숲", Detail = "테마", Args = """{"theme":"forest"}""", Swatch = "2E7D32" },
                    new CommandMenuSpec { Label = "바다", Detail = "테마", Args = """{"theme":"ocean"}""", Swatch = "0277BD" },
                    new CommandMenuSpec { Label = "와인", Detail = "테마", Args = """{"theme":"wine"}""", Swatch = "8E2430" },
                    new CommandMenuSpec { Label = "회색 돌", Detail = "테마", Args = """{"theme":"slate"}""", Swatch = "455A64" },
                    new CommandMenuSpec { Label = "노을", Detail = "테마", Args = """{"theme":"sunrise"}""", Swatch = "E65100" },
                    new CommandMenuSpec { Label = "보라", Detail = "테마", Args = """{"theme":"violet"}""", Swatch = "5E35B1" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.design.variant",
                Class = RibbonCommandClass.Tool,
                Tool = "set_deck_theme",
                Need = "slide",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "Office 테마", Detail = "색", Args = """{"colors":"office"}""" },
                    new CommandMenuSpec { Label = "문서 도우미 남색", Detail = "색", Args = """{"colors":"ink"}""" },
                    new CommandMenuSpec { Label = "숲", Detail = "색", Args = """{"colors":"forest"}""" },
                    new CommandMenuSpec { Label = "바다", Detail = "색", Args = """{"colors":"ocean"}""" },
                    new CommandMenuSpec { Label = "와인", Detail = "색", Args = """{"colors":"wine"}""" },
                    new CommandMenuSpec { Label = "회색 돌", Detail = "색", Args = """{"colors":"slate"}""" },
                    new CommandMenuSpec { Label = "노을", Detail = "색", Args = """{"colors":"sunrise"}""" },
                    new CommandMenuSpec { Label = "보라", Detail = "색", Args = """{"colors":"violet"}""" },
                    new CommandMenuSpec { Label = "맑은 고딕", Detail = "글꼴", Args = """{"fonts":"malgun"}""" },
                    new CommandMenuSpec { Label = "바탕 제목 + 맑은 고딕 본문", Detail = "글꼴", Args = """{"fonts":"serif_heading"}""" },
                    new CommandMenuSpec { Label = "돋움", Detail = "글꼴", Args = """{"fonts":"dotum"}""" },
                    new CommandMenuSpec { Label = "밝은 배경", Detail = "배경 스타일", Args = """{"background_style":"light"}""" },
                    new CommandMenuSpec { Label = "어두운 배경", Detail = "배경 스타일", Args = """{"background_style":"dark"}""" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.design.slideSize",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_size",
                Args = """{"preset":"{value}","width_cm":"{width_cm}","height_cm":"{height_cm}","scaling":"{scaling}"}""",
                Need = "slide",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "표준 (4:3)", Detail = "25.4 x 19.05 cm", Args = """{"preset":"standard_4_3"}""" },
                    new CommandMenuSpec { Label = "와이드스크린 (16:9)", Detail = "33.87 x 19.05 cm", Args = """{"preset":"widescreen_16_9"}""" },
                    new CommandMenuSpec { Label = "와이드스크린 (16:10)", Detail = "30.48 x 19.05 cm", Args = """{"preset":"widescreen_16_10"}""" },
                    new CommandMenuSpec { Label = "A4 용지", Detail = "29.7 x 21 cm", Args = """{"preset":"a4"}""" },
                    new CommandMenuSpec { Label = "레터 용지", Detail = "27.94 x 21.59 cm", Args = """{"preset":"letter"}""" },
                    new CommandMenuSpec { Label = "사용자 지정…", Detail = "창", Args = """{"preset":"custom"}""", Dialog = new DialogSpec("슬라이드 크기", new[] { new DialogField("width_cm", "너비(cm)", "number", "33.87", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("height_cm", "높이(cm)", "number", "19.05", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("scaling", "크기가 바뀔 때", "choice", "\"ensure_fit\"", new string[] { "ensure_fit", "maximize" }, new string[] { "맞춤 확인(내용이 안 잘려요)", "최대화(내용이 커져요)" }) }) },
                },
            },
            new CommandSpec
            {
                Id = "ppt.design.background",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_background",
                Args = """{"slides":"{scopeSlides}","fill":{"type":"{fill_type}","color":"{color}","color2":"{color2}","angle":"{angle}","transparency":"{transparency}","image_base64":"{image}"},"hide_graphics":"{hide_graphics}"}""",
                Need = "slide",
                Dialog = new DialogSpec("배경 서식", new[] { new DialogField("fill_type", "채우기", "choice", "\"solid\"", new string[] { "solid", "gradient", "picture", "inherit" }, new string[] { "단색", "그라데이션", "그림", "마스터를 따라가기" }), new DialogField("color", "색", "color", "\"#FFFFFF\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("color2", "두 번째 색", "color", "\"#EEF1F6\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("angle", "각도", "number", "90", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("transparency", "투명도(%)", "number", "0", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("image", "그림 파일", "text", null, System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("hide_graphics", "배경 그래픽 숨기기", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("scope", "적용 범위", "choice", "\"slide\"", new string[] { "slide", "all" }, new string[] { "이 슬라이드만", "모든 슬라이드에 적용" }) }),
            },
            new CommandSpec
            {
                Id = "ppt.design.designer",
                Class = RibbonCommandClass.Assistant,
                Prompt = "{slideText} 보기 좋게 다시 꾸며 줘. 글 내용은 바꾸지 말고 도형의 위치, 크기, 색, 글꼴만 정리해 줘.",
            },
            new CommandSpec
            {
                Id = "ppt.trans.preview",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"preview":"transition","slide":"{slide}"}}""",
            },
            new CommandSpec
            {
                Id = "ppt.trans.none",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","effect":"none"}""",
                Need = "slide",
                Pressed = "transition:none",
            },
            new CommandSpec
            {
                Id = "ppt.trans.fade",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","effect":"fade"}""",
                Need = "slide",
                Pressed = "transition:fade",
            },
            new CommandSpec
            {
                Id = "ppt.trans.push",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","effect":"push"}""",
                Need = "slide",
                Pressed = "transition:push",
            },
            new CommandSpec
            {
                Id = "ppt.trans.wipe",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","effect":"wipe"}""",
                Need = "slide",
                Pressed = "transition:wipe",
            },
            new CommandSpec
            {
                Id = "ppt.trans.split",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","effect":"split"}""",
                Need = "slide",
                Pressed = "transition:split",
            },
            new CommandSpec
            {
                Id = "ppt.trans.reveal",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","effect":"reveal"}""",
                Need = "slide",
                Pressed = "transition:reveal",
            },
            new CommandSpec
            {
                Id = "ppt.trans.options",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","option":"{value}"}""",
                Need = "slide",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "부드럽게", Args = """{"option":"smooth"}""", Effects = new[] { "fade" } },
                    new CommandMenuSpec { Label = "검정 화면으로 부드럽게", Args = """{"option":"through_black"}""", Effects = new[] { "fade" } },
                    new CommandMenuSpec { Label = "아래에서", Args = """{"option":"from_bottom"}""", Effects = new[] { "push", "wipe" } },
                    new CommandMenuSpec { Label = "왼쪽에서", Args = """{"option":"from_left"}""", Effects = new[] { "push", "wipe" } },
                    new CommandMenuSpec { Label = "오른쪽에서", Args = """{"option":"from_right"}""", Effects = new[] { "push", "wipe" } },
                    new CommandMenuSpec { Label = "위에서", Args = """{"option":"from_top"}""", Effects = new[] { "push", "wipe" } },
                    new CommandMenuSpec { Label = "세로 바깥쪽", Args = """{"option":"vertical_out"}""", Effects = new[] { "split" } },
                    new CommandMenuSpec { Label = "세로 안쪽", Args = """{"option":"vertical_in"}""", Effects = new[] { "split" } },
                    new CommandMenuSpec { Label = "가로 바깥쪽", Args = """{"option":"horizontal_out"}""", Effects = new[] { "split" } },
                    new CommandMenuSpec { Label = "가로 안쪽", Args = """{"option":"horizontal_in"}""", Effects = new[] { "split" } },
                    new CommandMenuSpec { Label = "왼쪽에서 부드럽게", Args = """{"option":"smooth_from_left"}""", Effects = new[] { "reveal" } },
                    new CommandMenuSpec { Label = "오른쪽에서 부드럽게", Args = """{"option":"smooth_from_right"}""", Effects = new[] { "reveal" } },
                    new CommandMenuSpec { Label = "왼쪽에서 검정 화면으로", Args = """{"option":"black_from_left"}""", Effects = new[] { "reveal" } },
                    new CommandMenuSpec { Label = "오른쪽에서 검정 화면으로", Args = """{"option":"black_from_right"}""", Effects = new[] { "reveal" } },
                },
            },
            new CommandSpec
            {
                Id = "ppt.trans.sound",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","sound":"{sound}"}""",
                Need = "slide",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "[소리 없음]", Args = """{"sound":{"type":"none"}}""" },
                    new CommandMenuSpec { Label = "기타 소리…", Args = """{"sound":{"type":"file","audio_base64":"{audio}"}}""", Picker = """{"title":"소리 파일 고르기","extensions":[".wav"],"maxMegabytes":5}""" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.trans.duration",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","duration":"{value}"}""",
                Need = "slide",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "기간 00.25", Args = """{"duration":0.25}""" },
                    new CommandMenuSpec { Label = "기간 00.50", Args = """{"duration":0.5}""" },
                    new CommandMenuSpec { Label = "기간 00.75", Args = """{"duration":0.75}""" },
                    new CommandMenuSpec { Label = "기간 01.00", Args = """{"duration":1.0}""" },
                    new CommandMenuSpec { Label = "기간 01.50", Args = """{"duration":1.5}""" },
                    new CommandMenuSpec { Label = "기간 02.00", Args = """{"duration":2.0}""" },
                    new CommandMenuSpec { Label = "기간 03.00", Args = """{"duration":3.0}""" },
                    new CommandMenuSpec { Label = "기간 04.00", Args = """{"duration":4.0}""" },
                    new CommandMenuSpec { Label = "기간 05.00", Args = """{"duration":5.0}""" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.trans.applyAll",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"all","copy_from":"{slide}"}""",
                Need = "slide",
            },
            new CommandSpec
            {
                Id = "ppt.trans.onClick",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","advance_on_click":true}""",
                ArgsOff = """{"advance_on_click":false}""",
                Need = "slide",
                Pressed = "advanceOnClick",
            },
            new CommandSpec
            {
                Id = "ppt.anim.preview",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"preview":"animation","slide":"{slide}"}}""",
            },
            new CommandSpec
            {
                Id = "ppt.anim.none",
                Class = RibbonCommandClass.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"remove","shapes":"{shapes}"}""",
                Need = "shape",
                Pressed = "animation:none",
            },
            new CommandSpec
            {
                Id = "ppt.anim.appear",
                Class = RibbonCommandClass.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"set","shapes":"{shapes}","phase":"entrance","effect":"appear"}""",
                Need = "shape",
                Pressed = "animation:appear",
            },
            new CommandSpec
            {
                Id = "ppt.anim.fade",
                Class = RibbonCommandClass.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"set","shapes":"{shapes}","phase":"entrance","effect":"fade"}""",
                Need = "shape",
                Pressed = "animation:fade",
            },
            new CommandSpec
            {
                Id = "ppt.anim.fly",
                Class = RibbonCommandClass.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"set","shapes":"{shapes}","phase":"entrance","effect":"fly_in"}""",
                Need = "shape",
                Pressed = "animation:fly_in",
            },
            new CommandSpec
            {
                Id = "ppt.anim.zoom",
                Class = RibbonCommandClass.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"set","shapes":"{shapes}","phase":"entrance","effect":"zoom"}""",
                Need = "shape",
                Pressed = "animation:zoom",
            },
            new CommandSpec
            {
                Id = "ppt.anim.options",
                Class = RibbonCommandClass.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"update","shapes":"{shapes}"}""",
                Need = "shape",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "아래쪽에서", Detail = "방향", Args = """{"option":"from_bottom"}""", Effects = new[] { "fly_in" } },
                    new CommandMenuSpec { Label = "왼쪽에서", Detail = "방향", Args = """{"option":"from_left"}""", Effects = new[] { "fly_in" } },
                    new CommandMenuSpec { Label = "위쪽에서", Detail = "방향", Args = """{"option":"from_top"}""", Effects = new[] { "fly_in" } },
                    new CommandMenuSpec { Label = "오른쪽에서", Detail = "방향", Args = """{"option":"from_right"}""", Effects = new[] { "fly_in" } },
                    new CommandMenuSpec { Label = "확대", Detail = "방향", Args = """{"option":"in"}""", Effects = new[] { "zoom" } },
                    new CommandMenuSpec { Label = "축소", Detail = "방향", Args = """{"option":"out"}""", Effects = new[] { "zoom" } },
                    new CommandMenuSpec { Label = "한 번에", Detail = "시퀀스", Args = """{"sequence":"as_one"}""", Need = "shape" },
                    new CommandMenuSpec { Label = "단락별", Detail = "시퀀스", Args = """{"sequence":"by_paragraph"}""", Need = "shape" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.anim.add",
                Class = RibbonCommandClass.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"add","shapes":"{shapes}","effect":"{effect}","phase":"{phase}"}""",
                Need = "shape",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "나타내기", Detail = "시작 효과", Args = """{"effect":"appear","phase":"entrance"}""" },
                    new CommandMenuSpec { Label = "밝기 변화", Detail = "시작 효과", Args = """{"effect":"fade","phase":"entrance"}""" },
                    new CommandMenuSpec { Label = "날아오기", Detail = "시작 효과", Args = """{"effect":"fly_in","phase":"entrance"}""" },
                    new CommandMenuSpec { Label = "확대/축소", Detail = "시작 효과", Args = """{"effect":"zoom","phase":"entrance"}""" },
                    new CommandMenuSpec { Label = "사라지기", Detail = "끝내기 효과", Args = """{"effect":"appear","phase":"exit"}""" },
                    new CommandMenuSpec { Label = "밝기 변화", Detail = "끝내기 효과", Args = """{"effect":"fade","phase":"exit"}""" },
                    new CommandMenuSpec { Label = "날아가기", Detail = "끝내기 효과", Args = """{"effect":"fly_in","phase":"exit"}""" },
                    new CommandMenuSpec { Label = "확대/축소", Detail = "끝내기 효과", Args = """{"effect":"zoom","phase":"exit"}""" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.anim.pane",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"animationPane":"toggle"}}""",
                Pressed = "animationPane",
            },
            new CommandSpec
            {
                Id = "ppt.anim.trigger",
                Class = RibbonCommandClass.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"update","shapes":"{shapes}","trigger":{"shape":"{value}"}}""",
                Need = "shape",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "순서대로 (기본)", Detail = "시작", Args = """{"trigger":{"shape":"none"}}""" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.anim.start",
                Class = RibbonCommandClass.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"update","shapes":"{shapes}","start":"{value}"}""",
                Need = "shape",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "클릭할 때", Args = """{"start":"on_click"}""" },
                    new CommandMenuSpec { Label = "이전 효과와 함께", Args = """{"start":"with_previous"}""" },
                    new CommandMenuSpec { Label = "이전 효과 다음에", Args = """{"start":"after_previous"}""" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.anim.duration",
                Class = RibbonCommandClass.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"update","shapes":"{shapes}","duration":"{value}"}""",
                Need = "shape",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "재생 시간 00.25", Args = """{"duration":0.25}""" },
                    new CommandMenuSpec { Label = "재생 시간 00.50", Args = """{"duration":0.5}""" },
                    new CommandMenuSpec { Label = "재생 시간 01.00", Args = """{"duration":1.0}""" },
                    new CommandMenuSpec { Label = "재생 시간 02.00", Args = """{"duration":2.0}""" },
                    new CommandMenuSpec { Label = "재생 시간 03.00", Args = """{"duration":3.0}""" },
                    new CommandMenuSpec { Label = "재생 시간 05.00", Args = """{"duration":5.0}""" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.show.fromStart",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"view":"show","showFrom":1}}""",
            },
            new CommandSpec
            {
                Id = "ppt.show.fromCurrent",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"view":"show","showFrom":"{slide}"}}""",
            },
            new CommandSpec
            {
                Id = "ppt.show.custom",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_show",
                Args = """{"custom_shows":[{"action":"save","name":"{name}","slides":"{pickedSlides}"}]}""",
                Need = "slide",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "사용자 지정 쇼…", Detail = "창", Dialog = new DialogSpec("사용자 지정 쇼", new[] { new DialogField("name", "쇼 이름", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("pickedSlides", "넣을 슬라이드 번호 (쉼표로 구분, 예: 1,3)", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }) },
                },
            },
            new CommandSpec
            {
                Id = "ppt.show.setup",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_show",
                Args = """{"show_type":"{show_type}","loop":"{loop}","show_animation":"{show_animation}","show_narration":"{show_narration}","use_timings":"{use_timings}","range":{"type":"{range}","from":"{range_from}","to":"{range_to}","custom_show":"{custom_show}"}}""",
                Need = "slide",
                Dialog = new DialogSpec("슬라이드 쇼 설정", new[] { new DialogField("show_type", "진행 방식", "choice", "\"presenter\"", new string[] { "presenter", "browse", "kiosk" }, new string[] { "발표자가 진행(전체 화면)", "웹 형식으로 진행(창)", "전시용(처음부터 반복)" }), new DialogField("loop", "[Esc]를 누를 때까지 반복", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("show_animation", "애니메이션 보이기", "bool", "true", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("show_narration", "내레이션 재생", "bool", "true", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("use_timings", "저장된 시간 사용", "bool", "true", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("range", "보여 줄 슬라이드", "choice", "\"all\"", new string[] { "all", "slides", "custom_show" }, new string[] { "전체", "시작~끝 번호", "사용자 지정 쇼" }), new DialogField("range_from", "시작 번호", "number", "1", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("range_to", "끝 번호", "number", "1", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("custom_show", "사용자 지정 쇼", "choice", null, new string[] { "names of the existing custom shows" }, new string[] { "names of the existing custom shows" }) }),
            },
            new CommandSpec
            {
                Id = "ppt.show.hide",
                Class = RibbonCommandClass.Tool,
                Tool = "arrange_slides",
                Args = """{"action":"hide","slides":"{slides}"}""",
                ArgsOff = """{"action":"show","slides":"{slides}"}""",
                Need = "slide",
                Pressed = "hidden",
            },
            new CommandSpec
            {
                Id = "ppt.show.rehearse",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"view":"show","showFrom":1,"rehearse":true}}""",
            },
            new CommandSpec
            {
                Id = "ppt.show.useTimings",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_show",
                Args = """{"use_timings":true}""",
                ArgsOff = """{"use_timings":false}""",
                Need = "slide",
                Pressed = "useTimings",
            },
            new CommandSpec
            {
                Id = "ppt.show.mediaControls",
                Class = RibbonCommandClass.Tool,
                Tool = "set_slide_show",
                Args = """{"show_media_controls":true}""",
                ArgsOff = """{"show_media_controls":false}""",
                Need = "slide",
                Pressed = "mediaControls",
            },
            new CommandSpec
            {
                Id = "ppt.show.presenterView",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"presenterView":"toggle"}}""",
                Pressed = "presenterView",
            },
            new CommandSpec
            {
                Id = "ppt.show.monitor",
                Class = RibbonCommandClass.Disabled,
                Reason = "여러 모니터 설정은 아직 지원하지 않아요. 슬라이드 쇼는 이 창 전체 화면으로 보여줘요.",
            },
            new CommandSpec
            {
                Id = "ppt.review.spelling",
                Class = RibbonCommandClass.Assistant,
                Prompt = "{scopeText} 맞춤법과 띄어쓰기를 검사하고 틀린 곳만 고쳐 줘. 뜻은 바꾸지 마.",
            },
            new CommandSpec
            {
                Id = "ppt.review.thesaurus",
                Class = RibbonCommandClass.Assistant,
                Prompt = "‘{selectionText}’와 뜻이 비슷한 말을 3개만 알려 줘. 바꾸고 싶으면 내가 고를게.",
            },
            new CommandSpec
            {
                Id = "ppt.review.accessibility",
                Class = RibbonCommandClass.Assistant,
                Prompt = "이 발표 자료의 접근성을 검사해 줘. 대체 텍스트가 없는 그림과 표, 글자 크기와 색 대비, 읽는 순서를 살펴보고 고칠 곳을 알려 줘.",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "접근성 검사", Detail = "검사", Prompt = "이 발표 자료의 접근성을 검사해 줘. 대체 텍스트가 없는 그림과 표, 글자 크기와 색 대비, 읽는 순서를 살펴보고 고칠 곳을 알려 줘." },
                    new CommandMenuSpec { Label = "대체 텍스트 채우기", Detail = "고치기", Prompt = "대체 텍스트가 없는 그림, 차트, 표를 찾아서 짧은 한국어 설명을 넣어 줘." },
                    new CommandMenuSpec { Label = "읽는 순서 정리", Detail = "고치기", Prompt = "슬라이드마다 읽는 순서가 자연스러운지 보고, 위에서 아래, 왼쪽에서 오른쪽 순서가 되도록 도형 순서를 정리해 줘." },
                },
            },
            new CommandSpec
            {
                Id = "ppt.review.translate",
                Class = RibbonCommandClass.Assistant,
                Prompt = "{scopeText} 글을 {language}로 번역해서 바꿔 줘. 글 구성과 서식은 그대로 두고 말만 옮겨 줘.",
                Dialog = new DialogSpec("번역", new[] { new DialogField("language", "어느 말로 옮길까요", "choice", "\"영어\"", new string[] { "영어", "일본어", "중국어(간체)", "중국어(번체)", "한국어" }, new string[] { "영어", "일본어", "중국어(간체)", "중국어(번체)", "한국어" }), new DialogField("scope", "범위", "choice", "\"slide\"", new string[] { "selection", "slide", "deck" }, new string[] { "고른 글상자", "이 슬라이드", "전체" }) }),
            },
            new CommandSpec
            {
                Id = "ppt.review.newComment",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_slide_comment",
                Args = """{"slide":"{slide}","action":"add","text":"{text}","author":"{userName}","shape":"{shape}"}""",
                Need = "slide",
                Dialog = new DialogSpec("새 메모", new[] { new DialogField("text", "메모", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "ppt.review.showComments",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"commentsPane":"toggle"}}""",
                Pressed = "commentsPane",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "메모 창 보기", Detail = "보기", Args = """{"set":{"commentsPane":"toggle"}}""" },
                    new CommandMenuSpec { Label = "이전 메모", Detail = "이동", Args = """{"set":{"commentStep":-1}}""", Need = "slide" },
                    new CommandMenuSpec { Label = "다음 메모", Detail = "이동", Args = """{"set":{"commentStep":1}}""", Need = "slide" },
                    new CommandMenuSpec { Label = "이 슬라이드 메모 모두 지우기", Detail = "지우기", Args = """{"slide":"{slide}","action":"clear"}""", Tool = "edit_slide_comment", Class = RibbonCommandClass.Tool, Need = "slide" },
                },
            },
            new CommandSpec
            {
                Id = "ppt.review.compare",
                Class = RibbonCommandClass.Assistant,
                Prompt = "지금 파일과 작업 폴더의 다른 .pptx 파일을 비교해서 달라진 슬라이드와 달라진 내용을 알려 줘. 비교할 파일이 정해지지 않았으면 먼저 물어봐 줘. 내용을 바꾸지는 마.",
            },
            new CommandSpec
            {
                Id = "ppt.view.normal",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"view":"normal"}}""",
                Pressed = "view:normal",
            },
            new CommandSpec
            {
                Id = "ppt.view.outline",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"view":"outline"}}""",
                Pressed = "view:outline",
            },
            new CommandSpec
            {
                Id = "ppt.view.sorter",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"view":"sorter"}}""",
                Pressed = "view:sorter",
            },
            new CommandSpec
            {
                Id = "ppt.view.notes",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"notesOpen":"toggle","view":"normal"}}""",
            },
            new CommandSpec
            {
                Id = "ppt.view.read",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"view":"read"}}""",
                Pressed = "view:read",
            },
            new CommandSpec
            {
                Id = "ppt.view.ruler",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"rulers":"toggle"}}""",
                Pressed = "rulers",
            },
            new CommandSpec
            {
                Id = "ppt.view.gridlines",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"gridlines":"toggle"}}""",
                Pressed = "gridlines",
            },
            new CommandSpec
            {
                Id = "ppt.view.guides",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"guides":"toggle"}}""",
                Pressed = "guides",
            },
            new CommandSpec
            {
                Id = "ppt.view.notesCheck",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"notesOpen":"toggle","view":"normal"}}""",
                Pressed = "notesOpen",
            },
            new CommandSpec
            {
                Id = "ppt.view.zoom",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"zoom":"{value}"}}""",
                Dialog = new DialogSpec("확대/축소", new[] { new DialogField("value", "배율", "choice", "100", new string[] { "200", "150", "100", "75", "66", "50", "fit", "custom" }, new string[] { "200", "150", "100", "75", "66", "50", "창에 맞춤", "직접 쓰기(50~200)" }) }),
            },
            new CommandSpec
            {
                Id = "ppt.view.fit",
                Class = RibbonCommandClass.View,
                Args = """{"set":{"zoom":"fit"}}""",
            },
        };
    }
}
