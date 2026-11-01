using System.Collections.Generic;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    internal static class PptCommandData
    {
        public static IReadOnlyList<PptCommandSpec> All { get; } = new[]
        {
            new PptCommandSpec
            {
                Id = "ppt.home.newSlide",
                Kind = PptCommandKind.Tool,
                Tool = "add_slide",
                Args = """{"after":"{slide}","layout":"same_as_current"}""",
                Menu = new[]
                {
                    new PptMenuSpec { Label = "제목 슬라이드", Detail = "표준 레이아웃", Args = """{"layout":"title"}""" },
                    new PptMenuSpec { Label = "제목 및 내용", Detail = "표준 레이아웃", Args = """{"layout":"title_content"}""" },
                    new PptMenuSpec { Label = "구역 머리글", Detail = "표준 레이아웃", Args = """{"layout":"section"}""" },
                    new PptMenuSpec { Label = "콘텐츠 2개", Detail = "표준 레이아웃", Args = """{"layout":"two_content"}""" },
                    new PptMenuSpec { Label = "비교", Detail = "표준 레이아웃", Args = """{"layout":"comparison"}""" },
                    new PptMenuSpec { Label = "제목만", Detail = "표준 레이아웃", Args = """{"layout":"title_only"}""" },
                    new PptMenuSpec { Label = "빈 화면", Detail = "표준 레이아웃", Args = """{"layout":"blank"}""" },
                    new PptMenuSpec { Label = "캡션 있는 콘텐츠", Detail = "표준 레이아웃", Args = """{"layout":"content_caption"}""" },
                    new PptMenuSpec { Label = "캡션 있는 그림", Detail = "표준 레이아웃", Args = """{"layout":"picture_caption"}""" },
                    new PptMenuSpec { Label = "선택한 슬라이드 복제", Detail = "슬라이드", Args = """{"action":"duplicate","slides":"{slides}"}""", Tool = "arrange_slides" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.home.layout",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_layout",
                Args = """{"slides":"{slides}","mode":"apply","layout":"{value}"}""",
                Need = PptNeed.Slide,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "제목 슬라이드", Detail = "표준 레이아웃", Args = """{"layout":"title"}""" },
                    new PptMenuSpec { Label = "제목 및 내용", Detail = "표준 레이아웃", Args = """{"layout":"title_content"}""" },
                    new PptMenuSpec { Label = "구역 머리글", Detail = "표준 레이아웃", Args = """{"layout":"section"}""" },
                    new PptMenuSpec { Label = "콘텐츠 2개", Detail = "표준 레이아웃", Args = """{"layout":"two_content"}""" },
                    new PptMenuSpec { Label = "비교", Detail = "표준 레이아웃", Args = """{"layout":"comparison"}""" },
                    new PptMenuSpec { Label = "제목만", Detail = "표준 레이아웃", Args = """{"layout":"title_only"}""" },
                    new PptMenuSpec { Label = "빈 화면", Detail = "표준 레이아웃", Args = """{"layout":"blank"}""" },
                    new PptMenuSpec { Label = "캡션 있는 콘텐츠", Detail = "표준 레이아웃", Args = """{"layout":"content_caption"}""" },
                    new PptMenuSpec { Label = "캡션 있는 그림", Detail = "표준 레이아웃", Args = """{"layout":"picture_caption"}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.home.reset",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_layout",
                Args = """{"slides":"{slides}","mode":"reset"}""",
                Need = PptNeed.Slide,
            },
            new PptCommandSpec
            {
                Id = "ppt.home.fontName",
                Kind = PptCommandKind.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","name":"{value}"}""",
                Need = PptNeed.Text,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "제목 글꼴", Detail = "테마 글꼴", Args = """{"name":"heading"}""" },
                    new PptMenuSpec { Label = "본문 글꼴", Detail = "테마 글꼴", Args = """{"name":"body"}""" },
                    new PptMenuSpec { Label = "맑은 고딕", Detail = "글꼴", Args = """{"name":"맑은 고딕"}""" },
                    new PptMenuSpec { Label = "굴림", Detail = "글꼴", Args = """{"name":"굴림"}""" },
                    new PptMenuSpec { Label = "돋움", Detail = "글꼴", Args = """{"name":"돋움"}""" },
                    new PptMenuSpec { Label = "바탕", Detail = "글꼴", Args = """{"name":"바탕"}""" },
                    new PptMenuSpec { Label = "궁서", Detail = "글꼴", Args = """{"name":"궁서"}""" },
                    new PptMenuSpec { Label = "함초롬돋움", Detail = "글꼴", Args = """{"name":"함초롬돋움"}""" },
                    new PptMenuSpec { Label = "함초롬바탕", Detail = "글꼴", Args = """{"name":"함초롬바탕"}""" },
                    new PptMenuSpec { Label = "나눔고딕", Detail = "글꼴", Args = """{"name":"나눔고딕"}""" },
                    new PptMenuSpec { Label = "나눔명조", Detail = "글꼴", Args = """{"name":"나눔명조"}""" },
                    new PptMenuSpec { Label = "Arial", Detail = "글꼴", Args = """{"name":"Arial"}""" },
                    new PptMenuSpec { Label = "Calibri", Detail = "글꼴", Args = """{"name":"Calibri"}""" },
                    new PptMenuSpec { Label = "Times New Roman", Detail = "글꼴", Args = """{"name":"Times New Roman"}""" },
                    new PptMenuSpec { Label = "Segoe UI", Detail = "글꼴", Args = """{"name":"Segoe UI"}""" },
                    new PptMenuSpec { Label = "Consolas", Detail = "글꼴", Args = """{"name":"Consolas"}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.home.fontSize",
                Kind = PptCommandKind.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","size":"{value}"}""",
                Need = PptNeed.Text,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "8", Args = """{"size":8}""" },
                    new PptMenuSpec { Label = "9", Args = """{"size":9}""" },
                    new PptMenuSpec { Label = "10", Args = """{"size":10}""" },
                    new PptMenuSpec { Label = "10.5", Args = """{"size":10.5}""" },
                    new PptMenuSpec { Label = "11", Args = """{"size":11}""" },
                    new PptMenuSpec { Label = "12", Args = """{"size":12}""" },
                    new PptMenuSpec { Label = "14", Args = """{"size":14}""" },
                    new PptMenuSpec { Label = "16", Args = """{"size":16}""" },
                    new PptMenuSpec { Label = "18", Args = """{"size":18}""" },
                    new PptMenuSpec { Label = "20", Args = """{"size":20}""" },
                    new PptMenuSpec { Label = "24", Args = """{"size":24}""" },
                    new PptMenuSpec { Label = "28", Args = """{"size":28}""" },
                    new PptMenuSpec { Label = "32", Args = """{"size":32}""" },
                    new PptMenuSpec { Label = "36", Args = """{"size":36}""" },
                    new PptMenuSpec { Label = "40", Args = """{"size":40}""" },
                    new PptMenuSpec { Label = "44", Args = """{"size":44}""" },
                    new PptMenuSpec { Label = "48", Args = """{"size":48}""" },
                    new PptMenuSpec { Label = "54", Args = """{"size":54}""" },
                    new PptMenuSpec { Label = "60", Args = """{"size":60}""" },
                    new PptMenuSpec { Label = "66", Args = """{"size":66}""" },
                    new PptMenuSpec { Label = "72", Args = """{"size":72}""" },
                    new PptMenuSpec { Label = "80", Args = """{"size":80}""" },
                    new PptMenuSpec { Label = "88", Args = """{"size":88}""" },
                    new PptMenuSpec { Label = "96", Args = """{"size":96}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.home.fontGrow",
                Kind = PptCommandKind.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","size_step":1}""",
                Need = PptNeed.Text,
            },
            new PptCommandSpec
            {
                Id = "ppt.home.fontShrink",
                Kind = PptCommandKind.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","size_step":-1}""",
                Need = PptNeed.Text,
            },
            new PptCommandSpec
            {
                Id = "ppt.home.bold",
                Kind = PptCommandKind.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","bold":true}""",
                ArgsOff = """{"bold":false}""",
                Need = PptNeed.Text,
                Pressed = "bold",
            },
            new PptCommandSpec
            {
                Id = "ppt.home.italic",
                Kind = PptCommandKind.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","italic":true}""",
                ArgsOff = """{"italic":false}""",
                Need = PptNeed.Text,
                Pressed = "italic",
            },
            new PptCommandSpec
            {
                Id = "ppt.home.underline",
                Kind = PptCommandKind.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","underline":true}""",
                ArgsOff = """{"underline":false}""",
                Need = PptNeed.Text,
                Pressed = "underline",
            },
            new PptCommandSpec
            {
                Id = "ppt.home.strike",
                Kind = PptCommandKind.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","strike":true}""",
                ArgsOff = """{"strike":false}""",
                Need = PptNeed.Text,
                Pressed = "strike",
            },
            new PptCommandSpec
            {
                Id = "ppt.home.fontColor",
                Kind = PptCommandKind.Tool,
                Tool = "set_font",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","range":"{range}","color":"{color}"}""",
                Need = PptNeed.Text,
                Dialog = """{"title":"글꼴 색","fields":[{"name":"color","label":"색","type":"color","default":"#B5462A","options":["theme:테마 색 (10열 × 6행)","standard:표준 색","auto:자동","hex:다른 색 직접 고르기"]}],"kind":"popover"}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.home.bullets",
                Kind = PptCommandKind.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","bullet":"bullet"}""",
                ArgsOff = """{"bullet":"none"}""",
                Need = PptNeed.Text,
                Pressed = "bullet",
            },
            new PptCommandSpec
            {
                Id = "ppt.home.numbers",
                Kind = PptCommandKind.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","bullet":"number","number_style":"arabic_period"}""",
                ArgsOff = """{"bullet":"none"}""",
                Need = PptNeed.Text,
                Pressed = "number",
            },
            new PptCommandSpec
            {
                Id = "ppt.home.outdent",
                Kind = PptCommandKind.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","level_change":-1}""",
                Need = PptNeed.Text,
            },
            new PptCommandSpec
            {
                Id = "ppt.home.indent",
                Kind = PptCommandKind.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","level_change":1}""",
                Need = PptNeed.Text,
            },
            new PptCommandSpec
            {
                Id = "ppt.home.lineSpacing",
                Kind = PptCommandKind.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","line_spacing":"{line_spacing}","space_before":"{space_before}","space_after":"{space_after}"}""",
                Need = PptNeed.Text,
                Dialog = """{"title":"줄 간격","fields":[{"name":"line_spacing","label":"줄 간격","type":"choice","default":1.0,"options":["1.0","1.15","1.5","2.0","2.5","3.0"]},{"name":"space_before","label":"단락 앞 간격(pt)","type":"number","default":0,"options":[]},{"name":"space_after","label":"단락 뒤 간격(pt)","type":"number","default":0,"options":[]}],"kind":"popover"}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.home.alignLeft",
                Kind = PptCommandKind.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","align":"left"}""",
                Need = PptNeed.Text,
                Pressed = "align:left",
            },
            new PptCommandSpec
            {
                Id = "ppt.home.alignCenter",
                Kind = PptCommandKind.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","align":"center"}""",
                ArgsOff = """{"align":"left"}""",
                Need = PptNeed.Text,
                Pressed = "align:center",
            },
            new PptCommandSpec
            {
                Id = "ppt.home.alignRight",
                Kind = PptCommandKind.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","align":"right"}""",
                ArgsOff = """{"align":"left"}""",
                Need = PptNeed.Text,
                Pressed = "align:right",
            },
            new PptCommandSpec
            {
                Id = "ppt.home.justify",
                Kind = PptCommandKind.Tool,
                Tool = "format_slide_paragraphs",
                Args = """{"slide":"{slide}","shape":"{shape}","paragraphs":"{paragraphs}","align":"justify"}""",
                ArgsOff = """{"align":"left"}""",
                Need = PptNeed.Text,
                Pressed = "align:justify",
            },
            new PptCommandSpec
            {
                Id = "ppt.home.shapes",
                Kind = PptCommandKind.Tool,
                Tool = "add_slide_shape",
                Args = """{"slide":"{slide}","shape_type":"{value}"}""",
                Need = PptNeed.Slide,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "선", Detail = "선", Args = """{"shape_type":"line"}""" },
                    new PptMenuSpec { Label = "화살표", Detail = "선", Args = """{"shape_type":"arrow_line"}""" },
                    new PptMenuSpec { Label = "양방향 화살표", Detail = "선", Args = """{"shape_type":"double_arrow_line"}""" },
                    new PptMenuSpec { Label = "꺾인 화살표", Detail = "선", Args = """{"shape_type":"elbow_arrow"}""" },
                    new PptMenuSpec { Label = "곡선 화살표", Detail = "선", Args = """{"shape_type":"curved_arrow"}""" },
                    new PptMenuSpec { Label = "텍스트 상자", Detail = "텍스트 상자", Args = """{"shape_type":"textbox"}""" },
                    new PptMenuSpec { Label = "사각형", Detail = "사각형", Args = """{"shape_type":"rect"}""" },
                    new PptMenuSpec { Label = "둥근 사각형", Detail = "사각형", Args = """{"shape_type":"round_rect"}""" },
                    new PptMenuSpec { Label = "타원", Detail = "기본 도형", Args = """{"shape_type":"ellipse"}""" },
                    new PptMenuSpec { Label = "이등변 삼각형", Detail = "기본 도형", Args = """{"shape_type":"triangle"}""" },
                    new PptMenuSpec { Label = "직각 삼각형", Detail = "기본 도형", Args = """{"shape_type":"right_triangle"}""" },
                    new PptMenuSpec { Label = "마름모", Detail = "기본 도형", Args = """{"shape_type":"diamond"}""" },
                    new PptMenuSpec { Label = "오각형", Detail = "기본 도형", Args = """{"shape_type":"pentagon"}""" },
                    new PptMenuSpec { Label = "육각형", Detail = "기본 도형", Args = """{"shape_type":"hexagon"}""" },
                    new PptMenuSpec { Label = "평행 사변형", Detail = "기본 도형", Args = """{"shape_type":"parallelogram"}""" },
                    new PptMenuSpec { Label = "사다리꼴", Detail = "기본 도형", Args = """{"shape_type":"trapezoid"}""" },
                    new PptMenuSpec { Label = "십자형", Detail = "기본 도형", Args = """{"shape_type":"cross"}""" },
                    new PptMenuSpec { Label = "도넛", Detail = "기본 도형", Args = """{"shape_type":"donut"}""" },
                    new PptMenuSpec { Label = "하트", Detail = "기본 도형", Args = """{"shape_type":"heart"}""" },
                    new PptMenuSpec { Label = "해", Detail = "기본 도형", Args = """{"shape_type":"sun"}""" },
                    new PptMenuSpec { Label = "달", Detail = "기본 도형", Args = """{"shape_type":"moon"}""" },
                    new PptMenuSpec { Label = "구름", Detail = "기본 도형", Args = """{"shape_type":"cloud"}""" },
                    new PptMenuSpec { Label = "왼쪽 중괄호", Detail = "기본 도형", Args = """{"shape_type":"left_brace"}""" },
                    new PptMenuSpec { Label = "오른쪽 중괄호", Detail = "기본 도형", Args = """{"shape_type":"right_brace"}""" },
                    new PptMenuSpec { Label = "왼쪽 대괄호", Detail = "기본 도형", Args = """{"shape_type":"left_bracket"}""" },
                    new PptMenuSpec { Label = "오른쪽 대괄호", Detail = "기본 도형", Args = """{"shape_type":"right_bracket"}""" },
                    new PptMenuSpec { Label = "오른쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"right_arrow"}""" },
                    new PptMenuSpec { Label = "왼쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"left_arrow"}""" },
                    new PptMenuSpec { Label = "위쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"up_arrow"}""" },
                    new PptMenuSpec { Label = "아래쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"down_arrow"}""" },
                    new PptMenuSpec { Label = "좌우 화살표", Detail = "블록 화살표", Args = """{"shape_type":"left_right_arrow"}""" },
                    new PptMenuSpec { Label = "갈매기형 수장", Detail = "블록 화살표", Args = """{"shape_type":"chevron"}""" },
                    new PptMenuSpec { Label = "오각형 화살표", Detail = "블록 화살표", Args = """{"shape_type":"home_plate"}""" },
                    new PptMenuSpec { Label = "순서도: 처리", Detail = "순서도", Args = """{"shape_type":"flow_process"}""" },
                    new PptMenuSpec { Label = "순서도: 판단", Detail = "순서도", Args = """{"shape_type":"flow_decision"}""" },
                    new PptMenuSpec { Label = "순서도: 터미널", Detail = "순서도", Args = """{"shape_type":"flow_terminator"}""" },
                    new PptMenuSpec { Label = "순서도: 문서", Detail = "순서도", Args = """{"shape_type":"flow_document"}""" },
                    new PptMenuSpec { Label = "별: 꼭짓점 5개", Detail = "별 및 현수막", Args = """{"shape_type":"star5"}""" },
                    new PptMenuSpec { Label = "별: 꼭짓점 6개", Detail = "별 및 현수막", Args = """{"shape_type":"star6"}""" },
                    new PptMenuSpec { Label = "말풍선: 사각형", Detail = "설명선", Args = """{"shape_type":"wedge_rect_callout"}""" },
                    new PptMenuSpec { Label = "말풍선: 둥근 사각형", Detail = "설명선", Args = """{"shape_type":"wedge_round_callout"}""" },
                    new PptMenuSpec { Label = "말풍선: 타원", Detail = "설명선", Args = """{"shape_type":"wedge_ellipse_callout"}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.home.arrange",
                Kind = PptCommandKind.Tool,
                Tool = "arrange_slide_shapes",
                Args = """{"slide":"{slide}","shapes":"{shapes}"}""",
                Need = PptNeed.Shape,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "맨 앞으로 가져오기", Detail = "순서", Args = """{"action":"order","order":"front"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "앞으로 가져오기", Detail = "순서", Args = """{"action":"order","order":"forward"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "맨 뒤로 보내기", Detail = "순서", Args = """{"action":"order","order":"back"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "뒤로 보내기", Detail = "순서", Args = """{"action":"order","order":"backward"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "왼쪽 맞춤", Detail = "맞춤", Args = """{"action":"align","align":"left"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "가운데 맞춤", Detail = "맞춤", Args = """{"action":"align","align":"center"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "오른쪽 맞춤", Detail = "맞춤", Args = """{"action":"align","align":"right"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "위쪽 맞춤", Detail = "맞춤", Args = """{"action":"align","align":"top"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "중간 맞춤", Detail = "맞춤", Args = """{"action":"align","align":"middle"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "아래쪽 맞춤", Detail = "맞춤", Args = """{"action":"align","align":"bottom"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "가로 간격을 동일하게", Detail = "간격", Args = """{"action":"distribute","distribute":"horizontal"}""", Need = PptNeed.Shapes3 },
                    new PptMenuSpec { Label = "세로 간격을 동일하게", Detail = "간격", Args = """{"action":"distribute","distribute":"vertical"}""", Need = PptNeed.Shapes3 },
                    new PptMenuSpec { Label = "그룹", Detail = "그룹", Args = """{"action":"group"}""", Need = PptNeed.Shapes2 },
                    new PptMenuSpec { Label = "그룹 해제", Detail = "그룹", Args = """{"action":"ungroup"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "오른쪽으로 90° 회전", Detail = "회전", Args = """{"action":"rotate","rotate":"cw90"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "왼쪽으로 90° 회전", Detail = "회전", Args = """{"action":"rotate","rotate":"ccw90"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "상하 대칭", Detail = "회전", Args = """{"action":"flip","flip":"vertical"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "좌우 대칭", Detail = "회전", Args = """{"action":"flip","flip":"horizontal"}""", Need = PptNeed.Shape },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.newSlide",
                Kind = PptCommandKind.Tool,
                Tool = "add_slide",
                Args = """{"after":"{slide}","layout":"same_as_current"}""",
                Menu = new[]
                {
                    new PptMenuSpec { Label = "제목 슬라이드", Detail = "표준 레이아웃", Args = """{"layout":"title"}""" },
                    new PptMenuSpec { Label = "제목 및 내용", Detail = "표준 레이아웃", Args = """{"layout":"title_content"}""" },
                    new PptMenuSpec { Label = "구역 머리글", Detail = "표준 레이아웃", Args = """{"layout":"section"}""" },
                    new PptMenuSpec { Label = "콘텐츠 2개", Detail = "표준 레이아웃", Args = """{"layout":"two_content"}""" },
                    new PptMenuSpec { Label = "비교", Detail = "표준 레이아웃", Args = """{"layout":"comparison"}""" },
                    new PptMenuSpec { Label = "제목만", Detail = "표준 레이아웃", Args = """{"layout":"title_only"}""" },
                    new PptMenuSpec { Label = "빈 화면", Detail = "표준 레이아웃", Args = """{"layout":"blank"}""" },
                    new PptMenuSpec { Label = "캡션 있는 콘텐츠", Detail = "표준 레이아웃", Args = """{"layout":"content_caption"}""" },
                    new PptMenuSpec { Label = "캡션 있는 그림", Detail = "표준 레이아웃", Args = """{"layout":"picture_caption"}""" },
                    new PptMenuSpec { Label = "선택한 슬라이드 복제", Detail = "슬라이드", Args = """{"action":"duplicate","slides":"{slides}"}""", Tool = "arrange_slides" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.table",
                Kind = PptCommandKind.Tool,
                Tool = "edit_slide_table",
                Args = """{"slide":"{slide}","action":"create","rows":"{rows}","columns":"{columns}","header_row":true,"banded_rows":true,"style":"medium_accent1"}""",
                Need = PptNeed.Slide,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "표 삽입…", Detail = "창", Dialog = """{"title":"표 넣기","fields":[{"name":"columns","label":"열 개수","type":"number","default":5,"options":[]},{"name":"rows","label":"행 개수","type":"number","default":4,"options":[]}]}""" },
                    new PptMenuSpec { Label = "엑셀 표 가져오기", Detail = "도우미", Kind = PptCommandKind.Assistant, Prompt = "{slideText} 엑셀 파일의 표를 가져와서 이 슬라이드에 표로 넣어 줘. 어느 범위를 넣을지 모르겠으면 먼저 물어봐 줘." },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.picture",
                Kind = PptCommandKind.Tool,
                Tool = "add_slide_picture",
                Args = """{"slide":"{slide}","image_base64":"{image}","name":"{imageName}"}""",
                Need = PptNeed.Slide,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "이 장치…", Detail = "파일 고르기", Picker = """{"title":"그림 고르기","extensions":[".png",".jpg",".jpeg",".gif",".bmp",".tif",".tiff"],"maxMegabytes":20}""" },
                    new PptMenuSpec { Label = "클립보드의 그림", Detail = "붙여넣기", Args = """{"image_base64":"{clipImage}","name":"clipboard.png"}""", Need = PptNeed.ClipboardImage },
                    new PptMenuSpec { Label = "그림 바꾸기", Detail = "선택한 그림", Args = """{"replace":"{shape}"}""", Picker = """{"title":"바꿀 그림 고르기","extensions":[".png",".jpg",".jpeg",".gif",".bmp",".tif",".tiff"],"maxMegabytes":20}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "스톡 이미지", Detail = "온라인", Kind = PptCommandKind.Disabled, Reason = "인터넷 그림 검색은 지원하지 않아요. 내 컴퓨터의 그림 파일을 골라 주세요." },
                    new PptMenuSpec { Label = "온라인 그림", Detail = "온라인", Kind = PptCommandKind.Disabled, Reason = "인터넷 그림 검색은 지원하지 않아요. 내 컴퓨터의 그림 파일을 골라 주세요." },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.shapes",
                Kind = PptCommandKind.Tool,
                Tool = "add_slide_shape",
                Args = """{"slide":"{slide}","shape_type":"{value}"}""",
                Need = PptNeed.Slide,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "선", Detail = "선", Args = """{"shape_type":"line"}""" },
                    new PptMenuSpec { Label = "화살표", Detail = "선", Args = """{"shape_type":"arrow_line"}""" },
                    new PptMenuSpec { Label = "양방향 화살표", Detail = "선", Args = """{"shape_type":"double_arrow_line"}""" },
                    new PptMenuSpec { Label = "꺾인 화살표", Detail = "선", Args = """{"shape_type":"elbow_arrow"}""" },
                    new PptMenuSpec { Label = "곡선 화살표", Detail = "선", Args = """{"shape_type":"curved_arrow"}""" },
                    new PptMenuSpec { Label = "텍스트 상자", Detail = "텍스트 상자", Args = """{"shape_type":"textbox"}""" },
                    new PptMenuSpec { Label = "사각형", Detail = "사각형", Args = """{"shape_type":"rect"}""" },
                    new PptMenuSpec { Label = "둥근 사각형", Detail = "사각형", Args = """{"shape_type":"round_rect"}""" },
                    new PptMenuSpec { Label = "타원", Detail = "기본 도형", Args = """{"shape_type":"ellipse"}""" },
                    new PptMenuSpec { Label = "이등변 삼각형", Detail = "기본 도형", Args = """{"shape_type":"triangle"}""" },
                    new PptMenuSpec { Label = "직각 삼각형", Detail = "기본 도형", Args = """{"shape_type":"right_triangle"}""" },
                    new PptMenuSpec { Label = "마름모", Detail = "기본 도형", Args = """{"shape_type":"diamond"}""" },
                    new PptMenuSpec { Label = "오각형", Detail = "기본 도형", Args = """{"shape_type":"pentagon"}""" },
                    new PptMenuSpec { Label = "육각형", Detail = "기본 도형", Args = """{"shape_type":"hexagon"}""" },
                    new PptMenuSpec { Label = "평행 사변형", Detail = "기본 도형", Args = """{"shape_type":"parallelogram"}""" },
                    new PptMenuSpec { Label = "사다리꼴", Detail = "기본 도형", Args = """{"shape_type":"trapezoid"}""" },
                    new PptMenuSpec { Label = "십자형", Detail = "기본 도형", Args = """{"shape_type":"cross"}""" },
                    new PptMenuSpec { Label = "도넛", Detail = "기본 도형", Args = """{"shape_type":"donut"}""" },
                    new PptMenuSpec { Label = "하트", Detail = "기본 도형", Args = """{"shape_type":"heart"}""" },
                    new PptMenuSpec { Label = "해", Detail = "기본 도형", Args = """{"shape_type":"sun"}""" },
                    new PptMenuSpec { Label = "달", Detail = "기본 도형", Args = """{"shape_type":"moon"}""" },
                    new PptMenuSpec { Label = "구름", Detail = "기본 도형", Args = """{"shape_type":"cloud"}""" },
                    new PptMenuSpec { Label = "왼쪽 중괄호", Detail = "기본 도형", Args = """{"shape_type":"left_brace"}""" },
                    new PptMenuSpec { Label = "오른쪽 중괄호", Detail = "기본 도형", Args = """{"shape_type":"right_brace"}""" },
                    new PptMenuSpec { Label = "왼쪽 대괄호", Detail = "기본 도형", Args = """{"shape_type":"left_bracket"}""" },
                    new PptMenuSpec { Label = "오른쪽 대괄호", Detail = "기본 도형", Args = """{"shape_type":"right_bracket"}""" },
                    new PptMenuSpec { Label = "오른쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"right_arrow"}""" },
                    new PptMenuSpec { Label = "왼쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"left_arrow"}""" },
                    new PptMenuSpec { Label = "위쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"up_arrow"}""" },
                    new PptMenuSpec { Label = "아래쪽 화살표", Detail = "블록 화살표", Args = """{"shape_type":"down_arrow"}""" },
                    new PptMenuSpec { Label = "좌우 화살표", Detail = "블록 화살표", Args = """{"shape_type":"left_right_arrow"}""" },
                    new PptMenuSpec { Label = "갈매기형 수장", Detail = "블록 화살표", Args = """{"shape_type":"chevron"}""" },
                    new PptMenuSpec { Label = "오각형 화살표", Detail = "블록 화살표", Args = """{"shape_type":"home_plate"}""" },
                    new PptMenuSpec { Label = "순서도: 처리", Detail = "순서도", Args = """{"shape_type":"flow_process"}""" },
                    new PptMenuSpec { Label = "순서도: 판단", Detail = "순서도", Args = """{"shape_type":"flow_decision"}""" },
                    new PptMenuSpec { Label = "순서도: 터미널", Detail = "순서도", Args = """{"shape_type":"flow_terminator"}""" },
                    new PptMenuSpec { Label = "순서도: 문서", Detail = "순서도", Args = """{"shape_type":"flow_document"}""" },
                    new PptMenuSpec { Label = "별: 꼭짓점 5개", Detail = "별 및 현수막", Args = """{"shape_type":"star5"}""" },
                    new PptMenuSpec { Label = "별: 꼭짓점 6개", Detail = "별 및 현수막", Args = """{"shape_type":"star6"}""" },
                    new PptMenuSpec { Label = "말풍선: 사각형", Detail = "설명선", Args = """{"shape_type":"wedge_rect_callout"}""" },
                    new PptMenuSpec { Label = "말풍선: 둥근 사각형", Detail = "설명선", Args = """{"shape_type":"wedge_round_callout"}""" },
                    new PptMenuSpec { Label = "말풍선: 타원", Detail = "설명선", Args = """{"shape_type":"wedge_ellipse_callout"}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.smartart",
                Kind = PptCommandKind.Tool,
                Tool = "add_slide_diagram",
                Args = """{"slide":"{slide}","kind":"{kind}","items":"{items}"}""",
                Need = PptNeed.Slide,
                Dialog = """{"title":"SmartArt 넣기","fields":[{"name":"kind","label":"종류","type":"choice","default":"process","options":["list:목록","process:프로세스","cycle:순환","hierarchy:계층 구조","pyramid:피라미드","matrix:행렬"]},{"name":"items","label":"항목(한 줄에 하나)","type":"text","default":"첫째\n둘째\n셋째","options":[],"multiline":true}]}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.chart",
                Kind = PptCommandKind.Tool,
                Tool = "edit_slide_chart",
                Args = """{"slide":"{slide}","action":"create","chart_type":"{chart_type}","title":"{title}","categories":"{categories}","series":"{series}","legend":"{legend}","data_labels":"{data_labels}"}""",
                Need = PptNeed.Slide,
                Dialog = """{"title":"차트 넣기","fields":[{"name":"chart_type","label":"종류","type":"choice","default":"column","options":["column:세로 막대","stacked_column:누적 세로 막대","bar:가로 막대","stacked_bar:누적 가로 막대","line:꺾은선","pie:원형","doughnut:도넛형","area:영역형"]},{"name":"title","label":"차트 제목","type":"text","default":"","options":[]},{"name":"data","label":"데이터(엑셀에서 복사해 붙여넣기)","type":"text","default":"\t매출\n1월\t3820\n2월\t3610\n3월\t4150","options":[],"multiline":true},{"name":"legend","label":"범례","type":"choice","default":"bottom","options":["none:없음","bottom:아래","right:오른쪽","top:위","left:왼쪽"]},{"name":"data_labels","label":"값 표시","type":"bool","default":false,"options":[]}]}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.link",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_link",
                Args = """{"slide":"{slide}","shape":"{shape}","range":"{range}"}""",
                Need = PptNeed.Shape,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "웹 주소…", Detail = "링크", Args = """{"target":{"type":"url","url":"{url}"}}""", Dialog = """{"title":"웹 주소 연결","fields":[{"name":"url","label":"웹 주소","type":"text","default":"https://","options":[]},{"name":"tooltip","label":"설명 말풍선","type":"text","default":"","options":[]}]}""" },
                    new PptMenuSpec { Label = "이 문서의 슬라이드…", Detail = "링크", Args = """{"target":{"type":"slide","slide":"{slideNumber}"}}""", Dialog = """{"title":"슬라이드 연결","fields":[{"name":"slideNumber","label":"슬라이드 번호","type":"number","default":"1","options":[]}]}""" },
                    new PptMenuSpec { Label = "이전 슬라이드", Detail = "링크", Args = """{"target":{"type":"previous_slide"}}""" },
                    new PptMenuSpec { Label = "다음 슬라이드", Detail = "링크", Args = """{"target":{"type":"next_slide"}}""" },
                    new PptMenuSpec { Label = "처음 슬라이드", Detail = "링크", Args = """{"target":{"type":"first_slide"}}""" },
                    new PptMenuSpec { Label = "마지막 슬라이드", Detail = "링크", Args = """{"target":{"type":"last_slide"}}""" },
                    new PptMenuSpec { Label = "전자 메일…", Detail = "링크", Args = """{"target":{"type":"email","address":"{address}","subject":"{subject}"}}""", Dialog = """{"title":"전자 메일 연결","fields":[{"name":"address","label":"받는 사람","type":"text","default":"","options":[]},{"name":"subject","label":"제목","type":"text","default":"","options":[]}]}""" },
                    new PptMenuSpec { Label = "링크 지우기", Detail = "링크", Args = """{"target":{"type":"none"}}""", Need = PptNeed.Shape },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.comment",
                Kind = PptCommandKind.Tool,
                Tool = "edit_slide_comment",
                Args = """{"slide":"{slide}","action":"add","text":"{text}","author":"{userName}","shape":"{shape}"}""",
                Need = PptNeed.Slide,
                Dialog = """{"title":"새 메모","fields":[{"name":"text","label":"메모","type":"text","default":"","options":[],"multiline":true}]}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.textbox",
                Kind = PptCommandKind.Tool,
                Tool = "add_slide_shape",
                Args = """{"slide":"{slide}","shape_type":"textbox"}""",
                Need = PptNeed.Slide,
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.headerFooter",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_footer",
                Args = """{"slides":"{scopeSlides}","footer":{"show":"{footer_show}","text":"{footer_text}"},"slide_number":"{slide_number}","date":{"show":"{date_show}","mode":"{date_mode}","format":"{date_format}","text":"{date_text}"},"skip_title_slide":"{skip_title_slide}"}""",
                Need = PptNeed.Slide,
                Dialog = """{"title":"머리글/바닥글","fields":[{"name":"date_show","label":"날짜 및 시간","type":"bool","default":false,"options":[]},{"name":"date_mode","label":"날짜 방식","type":"choice","default":"auto","options":["auto:자동으로 오늘 날짜","fixed:직접 쓰기"]},{"name":"date_format","label":"날짜 모양","type":"choice","default":"short","options":["short:2026-10-31","long:2026년 10월 31일 토요일","time:오후 4:30"]},{"name":"date_text","label":"직접 쓸 날짜","type":"text","default":"","options":[]},{"name":"slide_number","label":"슬라이드 번호","type":"bool","default":false,"options":[]},{"name":"footer_show","label":"바닥글","type":"bool","default":false,"options":[]},{"name":"footer_text","label":"바닥글 글","type":"text","default":"","options":[]},{"name":"skip_title_slide","label":"제목 슬라이드에는 보이지 않기","type":"bool","default":false,"options":[]},{"name":"scope","label":"적용 범위","type":"choice","default":"slide","options":["slide:이 슬라이드만","all:모든 슬라이드"]}],"buttons":["적용","모두 적용"],"note":"적용 = scope slide, 모두 적용 = scope all."}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.slideNumber",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_footer",
                Args = """{"slides":"{scopeSlides}","footer":{"show":"{footer_show}","text":"{footer_text}"},"slide_number":"{slide_number}","date":{"show":"{date_show}","mode":"{date_mode}","format":"{date_format}","text":"{date_text}"},"skip_title_slide":"{skip_title_slide}"}""",
                Need = PptNeed.Slide,
                Dialog = """{"title":"머리글/바닥글","fields":[{"name":"date_show","label":"날짜 및 시간","type":"bool","default":false,"options":[]},{"name":"date_mode","label":"날짜 방식","type":"choice","default":"auto","options":["auto:자동으로 오늘 날짜","fixed:직접 쓰기"]},{"name":"date_format","label":"날짜 모양","type":"choice","default":"short","options":["short:2026-10-31","long:2026년 10월 31일 토요일","time:오후 4:30"]},{"name":"date_text","label":"직접 쓸 날짜","type":"text","default":"","options":[]},{"name":"slide_number","label":"슬라이드 번호","type":"bool","default":false,"options":[]},{"name":"footer_show","label":"바닥글","type":"bool","default":false,"options":[]},{"name":"footer_text","label":"바닥글 글","type":"text","default":"","options":[]},{"name":"skip_title_slide","label":"제목 슬라이드에는 보이지 않기","type":"bool","default":false,"options":[]},{"name":"scope","label":"적용 범위","type":"choice","default":"slide","options":["slide:이 슬라이드만","all:모든 슬라이드"]}],"preset":{"slide_number":true},"buttons":["적용","모두 적용"]}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.dateTime",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_footer",
                Args = """{"slides":"{scopeSlides}","footer":{"show":"{footer_show}","text":"{footer_text}"},"slide_number":"{slide_number}","date":{"show":"{date_show}","mode":"{date_mode}","format":"{date_format}","text":"{date_text}"},"skip_title_slide":"{skip_title_slide}"}""",
                Need = PptNeed.Slide,
                Dialog = """{"title":"머리글/바닥글","fields":[{"name":"date_show","label":"날짜 및 시간","type":"bool","default":false,"options":[]},{"name":"date_mode","label":"날짜 방식","type":"choice","default":"auto","options":["auto:자동으로 오늘 날짜","fixed:직접 쓰기"]},{"name":"date_format","label":"날짜 모양","type":"choice","default":"short","options":["short:2026-10-31","long:2026년 10월 31일 토요일","time:오후 4:30"]},{"name":"date_text","label":"직접 쓸 날짜","type":"text","default":"","options":[]},{"name":"slide_number","label":"슬라이드 번호","type":"bool","default":false,"options":[]},{"name":"footer_show","label":"바닥글","type":"bool","default":false,"options":[]},{"name":"footer_text","label":"바닥글 글","type":"text","default":"","options":[]},{"name":"skip_title_slide","label":"제목 슬라이드에는 보이지 않기","type":"bool","default":false,"options":[]},{"name":"scope","label":"적용 범위","type":"choice","default":"slide","options":["slide:이 슬라이드만","all:모든 슬라이드"]}],"preset":{"date_show":true,"date_mode":"auto"},"buttons":["적용","모두 적용"]}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.insert.equation",
                Kind = PptCommandKind.Assistant,
                Prompt = "{slideText}에 수식을 글상자로 넣어 줘. 식은 이렇게: ",
                Menu = new[]
                {
                    new PptMenuSpec { Label = "분수", Detail = "수식", Prompt = "{slideText}에 분수 모양 수식을 글상자로 넣어 줘. 수식 내용은 이렇게: " },
                    new PptMenuSpec { Label = "위첨자 / 아래첨자", Detail = "수식", Prompt = "{slideText}에 위첨자나 아래첨자가 들어간 식을 글상자로 넣어 줘. 식은 이렇게: " },
                    new PptMenuSpec { Label = "근호", Detail = "수식", Prompt = "{slideText}에 제곱근 모양 식을 글상자로 넣어 줘. 식은 이렇게: " },
                    new PptMenuSpec { Label = "합계 / 적분", Detail = "수식", Prompt = "{slideText}에 합계(Σ)나 적분(∫) 식을 글상자로 넣어 줘. 식은 이렇게: " },
                    new PptMenuSpec { Label = "새 수식", Detail = "수식", Prompt = "{slideText}에 수식을 넣어 줘. 식은 이렇게: " },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.design.theme",
                Kind = PptCommandKind.Tool,
                Tool = "set_deck_theme",
                Args = """{"theme":"{value}"}""",
                Need = PptNeed.Slide,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "Office 테마", Detail = "테마", Args = """{"theme":"office"}""", Swatch = "4472C4" },
                    new PptMenuSpec { Label = "문서 도우미 남색", Detail = "테마", Args = """{"theme":"ink"}""", Swatch = "1C2A48" },
                    new PptMenuSpec { Label = "숲", Detail = "테마", Args = """{"theme":"forest"}""", Swatch = "2E7D32" },
                    new PptMenuSpec { Label = "바다", Detail = "테마", Args = """{"theme":"ocean"}""", Swatch = "0277BD" },
                    new PptMenuSpec { Label = "와인", Detail = "테마", Args = """{"theme":"wine"}""", Swatch = "8E2430" },
                    new PptMenuSpec { Label = "회색 돌", Detail = "테마", Args = """{"theme":"slate"}""", Swatch = "455A64" },
                    new PptMenuSpec { Label = "노을", Detail = "테마", Args = """{"theme":"sunrise"}""", Swatch = "E65100" },
                    new PptMenuSpec { Label = "보라", Detail = "테마", Args = """{"theme":"violet"}""", Swatch = "5E35B1" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.design.variant",
                Kind = PptCommandKind.Tool,
                Tool = "set_deck_theme",
                Need = PptNeed.Slide,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "Office 테마", Detail = "색", Args = """{"colors":"office"}""" },
                    new PptMenuSpec { Label = "문서 도우미 남색", Detail = "색", Args = """{"colors":"ink"}""" },
                    new PptMenuSpec { Label = "숲", Detail = "색", Args = """{"colors":"forest"}""" },
                    new PptMenuSpec { Label = "바다", Detail = "색", Args = """{"colors":"ocean"}""" },
                    new PptMenuSpec { Label = "와인", Detail = "색", Args = """{"colors":"wine"}""" },
                    new PptMenuSpec { Label = "회색 돌", Detail = "색", Args = """{"colors":"slate"}""" },
                    new PptMenuSpec { Label = "노을", Detail = "색", Args = """{"colors":"sunrise"}""" },
                    new PptMenuSpec { Label = "보라", Detail = "색", Args = """{"colors":"violet"}""" },
                    new PptMenuSpec { Label = "맑은 고딕", Detail = "글꼴", Args = """{"fonts":"malgun"}""" },
                    new PptMenuSpec { Label = "바탕 제목 + 맑은 고딕 본문", Detail = "글꼴", Args = """{"fonts":"serif_heading"}""" },
                    new PptMenuSpec { Label = "돋움", Detail = "글꼴", Args = """{"fonts":"dotum"}""" },
                    new PptMenuSpec { Label = "밝은 배경", Detail = "배경 스타일", Args = """{"background_style":"light"}""" },
                    new PptMenuSpec { Label = "어두운 배경", Detail = "배경 스타일", Args = """{"background_style":"dark"}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.design.slideSize",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_size",
                Args = """{"preset":"{value}","width_cm":"{width_cm}","height_cm":"{height_cm}","scaling":"{scaling}"}""",
                Need = PptNeed.Slide,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "표준 (4:3)", Detail = "25.4 x 19.05 cm", Args = """{"preset":"standard_4_3"}""" },
                    new PptMenuSpec { Label = "와이드스크린 (16:9)", Detail = "33.87 x 19.05 cm", Args = """{"preset":"widescreen_16_9"}""" },
                    new PptMenuSpec { Label = "와이드스크린 (16:10)", Detail = "30.48 x 19.05 cm", Args = """{"preset":"widescreen_16_10"}""" },
                    new PptMenuSpec { Label = "A4 용지", Detail = "29.7 x 21 cm", Args = """{"preset":"a4"}""" },
                    new PptMenuSpec { Label = "레터 용지", Detail = "27.94 x 21.59 cm", Args = """{"preset":"letter"}""" },
                    new PptMenuSpec { Label = "사용자 지정…", Detail = "창", Args = """{"preset":"custom"}""", Dialog = """{"title":"슬라이드 크기","fields":[{"name":"width_cm","label":"너비(cm)","type":"number","default":33.87,"options":[]},{"name":"height_cm","label":"높이(cm)","type":"number","default":19.05,"options":[]},{"name":"scaling","label":"크기가 바뀔 때","type":"choice","default":"ensure_fit","options":["ensure_fit:맞춤 확인(내용이 안 잘려요)","maximize:최대화(내용이 커져요)"]}]}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.design.background",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_background",
                Args = """{"slides":"{scopeSlides}","fill":{"type":"{fill_type}","color":"{color}","color2":"{color2}","angle":"{angle}","transparency":"{transparency}","image_base64":"{image}"},"hide_graphics":"{hide_graphics}"}""",
                Need = PptNeed.Slide,
                Dialog = """{"title":"배경 서식","fields":[{"name":"fill_type","label":"채우기","type":"choice","default":"solid","options":["solid:단색","gradient:그라데이션","picture:그림","inherit:마스터를 따라가기"]},{"name":"color","label":"색","type":"color","default":"#FFFFFF","options":[]},{"name":"color2","label":"두 번째 색","type":"color","default":"#EEF1F6","options":[]},{"name":"angle","label":"각도","type":"number","default":90,"options":[]},{"name":"transparency","label":"투명도(%)","type":"number","default":0,"options":[]},{"name":"image","label":"그림 파일","type":"text","default":null,"options":[],"picker":"image","extensions":[".png",".jpg",".jpeg",".gif",".bmp"]},{"name":"hide_graphics","label":"배경 그래픽 숨기기","type":"bool","default":false,"options":[]},{"name":"scope","label":"적용 범위","type":"choice","default":"slide","options":["slide:이 슬라이드만","all:모든 슬라이드에 적용"]}],"kind":"popover","buttons":["적용","모든 슬라이드에 적용"],"note":"Popover under the button (360px wide) like the other dialogs; the picker field shows the chosen file name. Initial values come from SlideModel.Background."}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.design.designer",
                Kind = PptCommandKind.Assistant,
                Prompt = "{slideText} 보기 좋게 다시 꾸며 줘. 글 내용은 바꾸지 말고 도형의 위치, 크기, 색, 글꼴만 정리해 줘.",
            },
            new PptCommandSpec
            {
                Id = "ppt.trans.preview",
                Kind = PptCommandKind.View,
                Args = """{"set":{"preview":"transition","slide":"{slide}"}}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.trans.none",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","effect":"none"}""",
                Need = PptNeed.Slide,
                Pressed = "transition:none",
            },
            new PptCommandSpec
            {
                Id = "ppt.trans.fade",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","effect":"fade"}""",
                Need = PptNeed.Slide,
                Pressed = "transition:fade",
            },
            new PptCommandSpec
            {
                Id = "ppt.trans.push",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","effect":"push"}""",
                Need = PptNeed.Slide,
                Pressed = "transition:push",
            },
            new PptCommandSpec
            {
                Id = "ppt.trans.wipe",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","effect":"wipe"}""",
                Need = PptNeed.Slide,
                Pressed = "transition:wipe",
            },
            new PptCommandSpec
            {
                Id = "ppt.trans.split",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","effect":"split"}""",
                Need = PptNeed.Slide,
                Pressed = "transition:split",
            },
            new PptCommandSpec
            {
                Id = "ppt.trans.reveal",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","effect":"reveal"}""",
                Need = PptNeed.Slide,
                Pressed = "transition:reveal",
            },
            new PptCommandSpec
            {
                Id = "ppt.trans.options",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","option":"{value}"}""",
                Need = PptNeed.Slide,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "부드럽게", Args = """{"option":"smooth"}""", Effects = new[] { "fade" } },
                    new PptMenuSpec { Label = "검정 화면으로 부드럽게", Args = """{"option":"through_black"}""", Effects = new[] { "fade" } },
                    new PptMenuSpec { Label = "아래에서", Args = """{"option":"from_bottom"}""", Effects = new[] { "push", "wipe" } },
                    new PptMenuSpec { Label = "왼쪽에서", Args = """{"option":"from_left"}""", Effects = new[] { "push", "wipe" } },
                    new PptMenuSpec { Label = "오른쪽에서", Args = """{"option":"from_right"}""", Effects = new[] { "push", "wipe" } },
                    new PptMenuSpec { Label = "위에서", Args = """{"option":"from_top"}""", Effects = new[] { "push", "wipe" } },
                    new PptMenuSpec { Label = "세로 바깥쪽", Args = """{"option":"vertical_out"}""", Effects = new[] { "split" } },
                    new PptMenuSpec { Label = "세로 안쪽", Args = """{"option":"vertical_in"}""", Effects = new[] { "split" } },
                    new PptMenuSpec { Label = "가로 바깥쪽", Args = """{"option":"horizontal_out"}""", Effects = new[] { "split" } },
                    new PptMenuSpec { Label = "가로 안쪽", Args = """{"option":"horizontal_in"}""", Effects = new[] { "split" } },
                    new PptMenuSpec { Label = "왼쪽에서 부드럽게", Args = """{"option":"smooth_from_left"}""", Effects = new[] { "reveal" } },
                    new PptMenuSpec { Label = "오른쪽에서 부드럽게", Args = """{"option":"smooth_from_right"}""", Effects = new[] { "reveal" } },
                    new PptMenuSpec { Label = "왼쪽에서 검정 화면으로", Args = """{"option":"black_from_left"}""", Effects = new[] { "reveal" } },
                    new PptMenuSpec { Label = "오른쪽에서 검정 화면으로", Args = """{"option":"black_from_right"}""", Effects = new[] { "reveal" } },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.trans.sound",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","sound":"{sound}"}""",
                Need = PptNeed.Slide,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "[소리 없음]", Args = """{"sound":{"type":"none"}}""" },
                    new PptMenuSpec { Label = "기타 소리…", Args = """{"sound":{"type":"file","audio_base64":"{audio}"}}""", Picker = """{"title":"소리 파일 고르기","extensions":[".wav"],"maxMegabytes":5}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.trans.duration",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","duration":"{value}"}""",
                Need = PptNeed.Slide,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "기간 00.25", Args = """{"duration":0.25}""" },
                    new PptMenuSpec { Label = "기간 00.50", Args = """{"duration":0.5}""" },
                    new PptMenuSpec { Label = "기간 00.75", Args = """{"duration":0.75}""" },
                    new PptMenuSpec { Label = "기간 01.00", Args = """{"duration":1.0}""" },
                    new PptMenuSpec { Label = "기간 01.50", Args = """{"duration":1.5}""" },
                    new PptMenuSpec { Label = "기간 02.00", Args = """{"duration":2.0}""" },
                    new PptMenuSpec { Label = "기간 03.00", Args = """{"duration":3.0}""" },
                    new PptMenuSpec { Label = "기간 04.00", Args = """{"duration":4.0}""" },
                    new PptMenuSpec { Label = "기간 05.00", Args = """{"duration":5.0}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.trans.applyAll",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"all","copy_from":"{slide}"}""",
                Need = PptNeed.Slide,
            },
            new PptCommandSpec
            {
                Id = "ppt.trans.onClick",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_transition",
                Args = """{"slides":"{slides}","advance_on_click":true}""",
                ArgsOff = """{"advance_on_click":false}""",
                Need = PptNeed.Slide,
                Pressed = "advanceOnClick",
            },
            new PptCommandSpec
            {
                Id = "ppt.anim.preview",
                Kind = PptCommandKind.View,
                Args = """{"set":{"preview":"animation","slide":"{slide}"}}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.anim.none",
                Kind = PptCommandKind.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"remove","shapes":"{shapes}"}""",
                Need = PptNeed.Shape,
                Pressed = "animation:none",
            },
            new PptCommandSpec
            {
                Id = "ppt.anim.appear",
                Kind = PptCommandKind.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"set","shapes":"{shapes}","phase":"entrance","effect":"appear"}""",
                Need = PptNeed.Shape,
                Pressed = "animation:appear",
            },
            new PptCommandSpec
            {
                Id = "ppt.anim.fade",
                Kind = PptCommandKind.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"set","shapes":"{shapes}","phase":"entrance","effect":"fade"}""",
                Need = PptNeed.Shape,
                Pressed = "animation:fade",
            },
            new PptCommandSpec
            {
                Id = "ppt.anim.fly",
                Kind = PptCommandKind.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"set","shapes":"{shapes}","phase":"entrance","effect":"fly_in"}""",
                Need = PptNeed.Shape,
                Pressed = "animation:fly_in",
            },
            new PptCommandSpec
            {
                Id = "ppt.anim.zoom",
                Kind = PptCommandKind.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"set","shapes":"{shapes}","phase":"entrance","effect":"zoom"}""",
                Need = PptNeed.Shape,
                Pressed = "animation:zoom",
            },
            new PptCommandSpec
            {
                Id = "ppt.anim.options",
                Kind = PptCommandKind.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"update","shapes":"{shapes}"}""",
                Need = PptNeed.Shape,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "아래쪽에서", Detail = "방향", Args = """{"option":"from_bottom"}""", Effects = new[] { "fly_in" } },
                    new PptMenuSpec { Label = "왼쪽에서", Detail = "방향", Args = """{"option":"from_left"}""", Effects = new[] { "fly_in" } },
                    new PptMenuSpec { Label = "위쪽에서", Detail = "방향", Args = """{"option":"from_top"}""", Effects = new[] { "fly_in" } },
                    new PptMenuSpec { Label = "오른쪽에서", Detail = "방향", Args = """{"option":"from_right"}""", Effects = new[] { "fly_in" } },
                    new PptMenuSpec { Label = "확대", Detail = "방향", Args = """{"option":"in"}""", Effects = new[] { "zoom" } },
                    new PptMenuSpec { Label = "축소", Detail = "방향", Args = """{"option":"out"}""", Effects = new[] { "zoom" } },
                    new PptMenuSpec { Label = "한 번에", Detail = "시퀀스", Args = """{"sequence":"as_one"}""", Need = PptNeed.Shape },
                    new PptMenuSpec { Label = "단락별", Detail = "시퀀스", Args = """{"sequence":"by_paragraph"}""", Need = PptNeed.Shape },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.anim.add",
                Kind = PptCommandKind.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"add","shapes":"{shapes}","effect":"{effect}","phase":"{phase}"}""",
                Need = PptNeed.Shape,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "나타내기", Detail = "시작 효과", Args = """{"effect":"appear","phase":"entrance"}""" },
                    new PptMenuSpec { Label = "밝기 변화", Detail = "시작 효과", Args = """{"effect":"fade","phase":"entrance"}""" },
                    new PptMenuSpec { Label = "날아오기", Detail = "시작 효과", Args = """{"effect":"fly_in","phase":"entrance"}""" },
                    new PptMenuSpec { Label = "확대/축소", Detail = "시작 효과", Args = """{"effect":"zoom","phase":"entrance"}""" },
                    new PptMenuSpec { Label = "사라지기", Detail = "끝내기 효과", Args = """{"effect":"appear","phase":"exit"}""" },
                    new PptMenuSpec { Label = "밝기 변화", Detail = "끝내기 효과", Args = """{"effect":"fade","phase":"exit"}""" },
                    new PptMenuSpec { Label = "날아가기", Detail = "끝내기 효과", Args = """{"effect":"fly_in","phase":"exit"}""" },
                    new PptMenuSpec { Label = "확대/축소", Detail = "끝내기 효과", Args = """{"effect":"zoom","phase":"exit"}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.anim.pane",
                Kind = PptCommandKind.View,
                Args = """{"set":{"animationPane":"toggle"}}""",
                Pressed = "animationPane",
            },
            new PptCommandSpec
            {
                Id = "ppt.anim.trigger",
                Kind = PptCommandKind.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"update","shapes":"{shapes}","trigger":{"shape":"{value}"}}""",
                Need = PptNeed.Shape,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "순서대로 (기본)", Detail = "시작", Args = """{"trigger":{"shape":"none"}}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.anim.start",
                Kind = PptCommandKind.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"update","shapes":"{shapes}","start":"{value}"}""",
                Need = PptNeed.Shape,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "클릭할 때", Args = """{"start":"on_click"}""" },
                    new PptMenuSpec { Label = "이전 효과와 함께", Args = """{"start":"with_previous"}""" },
                    new PptMenuSpec { Label = "이전 효과 다음에", Args = """{"start":"after_previous"}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.anim.duration",
                Kind = PptCommandKind.Tool,
                Tool = "set_shape_animation",
                Args = """{"slide":"{slide}","action":"update","shapes":"{shapes}","duration":"{value}"}""",
                Need = PptNeed.Shape,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "재생 시간 00.25", Args = """{"duration":0.25}""" },
                    new PptMenuSpec { Label = "재생 시간 00.50", Args = """{"duration":0.5}""" },
                    new PptMenuSpec { Label = "재생 시간 01.00", Args = """{"duration":1.0}""" },
                    new PptMenuSpec { Label = "재생 시간 02.00", Args = """{"duration":2.0}""" },
                    new PptMenuSpec { Label = "재생 시간 03.00", Args = """{"duration":3.0}""" },
                    new PptMenuSpec { Label = "재생 시간 05.00", Args = """{"duration":5.0}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.show.fromStart",
                Kind = PptCommandKind.View,
                Args = """{"set":{"view":"show","showFrom":1}}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.show.fromCurrent",
                Kind = PptCommandKind.View,
                Args = """{"set":{"view":"show","showFrom":"{slide}"}}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.show.custom",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_show",
                Args = """{"custom_shows":[{"action":"save","name":"{name}","slides":"{pickedSlides}"}]}""",
                Need = PptNeed.Slide,
                Menu = new[]
                {
                    new PptMenuSpec { Label = "사용자 지정 쇼…", Detail = "창", Dialog = """{"title":"사용자 지정 쇼","fields":[{"name":"name","label":"쇼 이름","type":"text","default":"","options":[]},{"name":"pickedSlides","label":"넣을 슬라이드 번호 (쉼표로 구분, 예: 1,3)","type":"text","default":"","options":[]}],"buttons":["저장","지우기"]}""" },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.show.setup",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_show",
                Args = """{"show_type":"{show_type}","loop":"{loop}","show_animation":"{show_animation}","show_narration":"{show_narration}","use_timings":"{use_timings}","range":{"type":"{range}","from":"{range_from}","to":"{range_to}","custom_show":"{custom_show}"}}""",
                Need = PptNeed.Slide,
                Dialog = """{"title":"슬라이드 쇼 설정","fields":[{"name":"show_type","label":"진행 방식","type":"choice","default":"presenter","options":["presenter:발표자가 진행(전체 화면)","browse:웹 형식으로 진행(창)","kiosk:전시용(처음부터 반복)"]},{"name":"loop","label":"[Esc]를 누를 때까지 반복","type":"bool","default":false,"options":[]},{"name":"show_animation","label":"애니메이션 보이기","type":"bool","default":true,"options":[]},{"name":"show_narration","label":"내레이션 재생","type":"bool","default":true,"options":[]},{"name":"use_timings","label":"저장된 시간 사용","type":"bool","default":true,"options":[]},{"name":"range","label":"보여 줄 슬라이드","type":"choice","default":"all","options":["all:전체","slides:시작~끝 번호","custom_show:사용자 지정 쇼"]},{"name":"range_from","label":"시작 번호","type":"number","default":1,"options":[]},{"name":"range_to","label":"끝 번호","type":"number","default":1,"options":[]},{"name":"custom_show","label":"사용자 지정 쇼","type":"choice","default":"","options":["names of the existing custom shows"]}],"kind":"popover","note":"range_from/range_to show only for range slides, custom_show only for range custom_show. Initial values come from p:showPr."}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.show.hide",
                Kind = PptCommandKind.Tool,
                Tool = "arrange_slides",
                Args = """{"action":"hide","slides":"{slides}"}""",
                ArgsOff = """{"action":"show","slides":"{slides}"}""",
                Need = PptNeed.Slide,
                Pressed = "hidden",
            },
            new PptCommandSpec
            {
                Id = "ppt.show.rehearse",
                Kind = PptCommandKind.View,
                Args = """{"set":{"view":"show","showFrom":1,"rehearse":true}}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.show.useTimings",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_show",
                Args = """{"use_timings":true}""",
                ArgsOff = """{"use_timings":false}""",
                Need = PptNeed.Slide,
                Pressed = "useTimings",
            },
            new PptCommandSpec
            {
                Id = "ppt.show.mediaControls",
                Kind = PptCommandKind.Tool,
                Tool = "set_slide_show",
                Args = """{"show_media_controls":true}""",
                ArgsOff = """{"show_media_controls":false}""",
                Need = PptNeed.Slide,
                Pressed = "mediaControls",
            },
            new PptCommandSpec
            {
                Id = "ppt.show.presenterView",
                Kind = PptCommandKind.View,
                Args = """{"set":{"presenterView":"toggle"}}""",
                Pressed = "presenterView",
            },
            new PptCommandSpec
            {
                Id = "ppt.show.monitor",
                Kind = PptCommandKind.Disabled,
                Reason = "여러 모니터 설정은 아직 지원하지 않아요. 슬라이드 쇼는 이 창 전체 화면으로 보여줘요.",
            },
            new PptCommandSpec
            {
                Id = "ppt.review.spelling",
                Kind = PptCommandKind.Assistant,
                Prompt = "{scopeText} 맞춤법과 띄어쓰기를 검사하고 틀린 곳만 고쳐 줘. 뜻은 바꾸지 마.",
            },
            new PptCommandSpec
            {
                Id = "ppt.review.thesaurus",
                Kind = PptCommandKind.Assistant,
                Prompt = "‘{selectionText}’와 뜻이 비슷한 말을 3개만 알려 줘. 바꾸고 싶으면 내가 고를게.",
            },
            new PptCommandSpec
            {
                Id = "ppt.review.accessibility",
                Kind = PptCommandKind.Assistant,
                Prompt = "이 발표 자료의 접근성을 검사해 줘. 대체 텍스트가 없는 그림과 표, 글자 크기와 색 대비, 읽는 순서를 살펴보고 고칠 곳을 알려 줘.",
                Menu = new[]
                {
                    new PptMenuSpec { Label = "접근성 검사", Detail = "검사", Prompt = "이 발표 자료의 접근성을 검사해 줘. 대체 텍스트가 없는 그림과 표, 글자 크기와 색 대비, 읽는 순서를 살펴보고 고칠 곳을 알려 줘." },
                    new PptMenuSpec { Label = "대체 텍스트 채우기", Detail = "고치기", Prompt = "대체 텍스트가 없는 그림, 차트, 표를 찾아서 짧은 한국어 설명을 넣어 줘." },
                    new PptMenuSpec { Label = "읽는 순서 정리", Detail = "고치기", Prompt = "슬라이드마다 읽는 순서가 자연스러운지 보고, 위에서 아래, 왼쪽에서 오른쪽 순서가 되도록 도형 순서를 정리해 줘." },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.review.translate",
                Kind = PptCommandKind.Assistant,
                Prompt = "{scopeText} 글을 {language}로 번역해서 바꿔 줘. 글 구성과 서식은 그대로 두고 말만 옮겨 줘.",
                Dialog = """{"title":"번역","fields":[{"name":"language","label":"어느 말로 옮길까요","type":"choice","default":"영어","options":["영어","일본어","중국어(간체)","중국어(번체)","한국어"]},{"name":"scope","label":"범위","type":"choice","default":"slide","options":["selection:고른 글상자","slide:이 슬라이드","deck:전체"]}]}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.review.newComment",
                Kind = PptCommandKind.Tool,
                Tool = "edit_slide_comment",
                Args = """{"slide":"{slide}","action":"add","text":"{text}","author":"{userName}","shape":"{shape}"}""",
                Need = PptNeed.Slide,
                Dialog = """{"title":"새 메모","fields":[{"name":"text","label":"메모","type":"text","default":"","options":[],"multiline":true}]}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.review.showComments",
                Kind = PptCommandKind.View,
                Args = """{"set":{"commentsPane":"toggle"}}""",
                Pressed = "commentsPane",
                Menu = new[]
                {
                    new PptMenuSpec { Label = "메모 창 보기", Detail = "보기", Args = """{"set":{"commentsPane":"toggle"}}""" },
                    new PptMenuSpec { Label = "이전 메모", Detail = "이동", Args = """{"set":{"commentStep":-1}}""", Need = PptNeed.Slide },
                    new PptMenuSpec { Label = "다음 메모", Detail = "이동", Args = """{"set":{"commentStep":1}}""", Need = PptNeed.Slide },
                    new PptMenuSpec { Label = "이 슬라이드 메모 모두 지우기", Detail = "지우기", Args = """{"slide":"{slide}","action":"clear"}""", Tool = "edit_slide_comment", Kind = PptCommandKind.Tool, Need = PptNeed.Slide },
                },
            },
            new PptCommandSpec
            {
                Id = "ppt.review.compare",
                Kind = PptCommandKind.Assistant,
                Prompt = "지금 파일과 ‘{file}’을(를) 비교해서 달라진 슬라이드와 달라진 내용을 알려 줘. 내용을 바꾸지는 마.",
                Dialog = """{"title":"비교","fields":[{"name":"file","label":"비교할 파일","type":"choice","default":"","options":["작업 폴더의 다른 .pptx 파일"]}]}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.view.normal",
                Kind = PptCommandKind.View,
                Args = """{"set":{"view":"normal"}}""",
                Pressed = "view:normal",
            },
            new PptCommandSpec
            {
                Id = "ppt.view.outline",
                Kind = PptCommandKind.View,
                Args = """{"set":{"view":"outline"}}""",
                Pressed = "view:outline",
            },
            new PptCommandSpec
            {
                Id = "ppt.view.sorter",
                Kind = PptCommandKind.View,
                Args = """{"set":{"view":"sorter"}}""",
                Pressed = "view:sorter",
            },
            new PptCommandSpec
            {
                Id = "ppt.view.notes",
                Kind = PptCommandKind.View,
                Args = """{"set":{"notesOpen":"toggle","view":"normal"}}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.view.read",
                Kind = PptCommandKind.View,
                Args = """{"set":{"view":"read"}}""",
                Pressed = "view:read",
            },
            new PptCommandSpec
            {
                Id = "ppt.view.ruler",
                Kind = PptCommandKind.View,
                Args = """{"set":{"rulers":"toggle"}}""",
                Pressed = "rulers",
            },
            new PptCommandSpec
            {
                Id = "ppt.view.gridlines",
                Kind = PptCommandKind.View,
                Args = """{"set":{"gridlines":"toggle"}}""",
                Pressed = "gridlines",
            },
            new PptCommandSpec
            {
                Id = "ppt.view.guides",
                Kind = PptCommandKind.View,
                Args = """{"set":{"guides":"toggle"}}""",
                Pressed = "guides",
            },
            new PptCommandSpec
            {
                Id = "ppt.view.notesCheck",
                Kind = PptCommandKind.View,
                Args = """{"set":{"notesOpen":"toggle","view":"normal"}}""",
                Pressed = "notesOpen",
            },
            new PptCommandSpec
            {
                Id = "ppt.view.zoom",
                Kind = PptCommandKind.View,
                Args = """{"set":{"zoom":"{value}"}}""",
                Dialog = """{"title":"확대/축소","fields":[{"name":"value","label":"배율","type":"choice","default":100,"options":["200","150","100","75","66","50","fit:창에 맞춤","custom:직접 쓰기(50~200)"]}],"kind":"popover"}""",
            },
            new PptCommandSpec
            {
                Id = "ppt.view.fit",
                Kind = PptCommandKind.View,
                Args = """{"set":{"zoom":"fit"}}""",
            },
        };
    }
}
