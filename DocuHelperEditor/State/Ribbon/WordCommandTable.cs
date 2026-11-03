using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    internal static class WordCommandTable
    {
        public static IReadOnlyList<CommandSpec> All { get; } = new[]
        {
            new CommandSpec
            {
                Id = "word.home.clipboard.paste",
                Label = "붙여넣기",
                Class = RibbonCommandClass.Clipboard,
                Action = "paste",
                Tool = "edit_text",
                Args = """{"ops":[{"op":"paste","id":"{id}","offset":"{offset}","paragraphs":"{clip paragraphs}"}],"mode":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "원래 서식 유지", Args = """{"mode":"keep_source"}""", Detail = "내부 복사본이면 글자·문단 서식을 그대로. 바깥 HTML이면 굵게·기울임·밑줄·글자 크기·색·목록·표를 가져와요" },
                    new CommandMenuSpec { Label = "서식 병합", Args = """{"mode":"merge"}""", Detail = "글은 가져오되 서식은 붙여넣는 자리의 서식을 따라요" },
                    new CommandMenuSpec { Label = "텍스트만 유지", Args = """{"mode":"text_only"}""", Detail = "Ctrl+Shift+V. 서식 없이 글만" },
                },
            },
            new CommandSpec
            {
                Id = "word.home.clipboard.cut",
                Label = "잘라내기",
                Class = RibbonCommandClass.Clipboard,
                Action = "cut",
                Tool = "edit_text",
                Args = """{"ops":[{"op":"delete","id":"{id}","start":"{start}","end":"{end}"}]}""",
            },
            new CommandSpec
            {
                Id = "word.home.clipboard.copy",
                Label = "복사",
                Class = RibbonCommandClass.Clipboard,
                Action = "copy",
            },
            new CommandSpec
            {
                Id = "word.home.clipboard.formatpainter",
                Label = "서식 복사",
                Class = RibbonCommandClass.Clipboard,
                Action = "formatPainter",
                Tool = "format_text",
                Args = """{"targets":"{targets}","painter":"{captured char props}","paragraph_painter":"{captured paragraph props → format_paragraph}"}""",
            },
            new CommandSpec
            {
                Id = "word.home.font.name",
                Label = "글꼴",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","font":"{value}"}""",
                Value = "fontName",
                MenuStyle = "fontName",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "맑은 고딕", Args = """{}""" },
                    new CommandMenuSpec { Label = "바탕", Args = """{}""" },
                    new CommandMenuSpec { Label = "돋움", Args = """{}""" },
                    new CommandMenuSpec { Label = "굴림", Args = """{}""" },
                    new CommandMenuSpec { Label = "궁서", Args = """{}""" },
                    new CommandMenuSpec { Label = "함초롬바탕", Args = """{}""" },
                    new CommandMenuSpec { Label = "함초롬돋움", Args = """{}""" },
                    new CommandMenuSpec { Label = "나눔고딕", Args = """{}""" },
                    new CommandMenuSpec { Label = "Arial", Args = """{}""" },
                    new CommandMenuSpec { Label = "Times New Roman", Args = """{}""" },
                    new CommandMenuSpec { Label = "Calibri", Args = """{}""" },
                    new CommandMenuSpec { Label = "Cambria", Args = """{}""" },
                    new CommandMenuSpec { Label = "Segoe UI", Args = """{}""" },
                    new CommandMenuSpec { Label = "Courier New", Args = """{}""" },
                },
                Dialog = new DialogSpec("글꼴 고르기", new[] { new DialogField("value", "글꼴 이름", "text", "\"맑은 고딕\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.home.font.size",
                Label = "글꼴 크기",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","size":"{value}"}""",
                Value = "fontSize",
                MenuStyle = "fontSize",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "8", Args = """{}""" },
                    new CommandMenuSpec { Label = "9", Args = """{}""" },
                    new CommandMenuSpec { Label = "10", Args = """{}""" },
                    new CommandMenuSpec { Label = "10.5", Args = """{}""" },
                    new CommandMenuSpec { Label = "11", Args = """{}""" },
                    new CommandMenuSpec { Label = "12", Args = """{}""" },
                    new CommandMenuSpec { Label = "14", Args = """{}""" },
                    new CommandMenuSpec { Label = "16", Args = """{}""" },
                    new CommandMenuSpec { Label = "18", Args = """{}""" },
                    new CommandMenuSpec { Label = "20", Args = """{}""" },
                    new CommandMenuSpec { Label = "22", Args = """{}""" },
                    new CommandMenuSpec { Label = "24", Args = """{}""" },
                    new CommandMenuSpec { Label = "26", Args = """{}""" },
                    new CommandMenuSpec { Label = "28", Args = """{}""" },
                    new CommandMenuSpec { Label = "36", Args = """{}""" },
                    new CommandMenuSpec { Label = "48", Args = """{}""" },
                    new CommandMenuSpec { Label = "72", Args = """{}""" },
                },
                Dialog = new DialogSpec("글꼴 크기", new[] { new DialogField("value", "크기(pt)", "number", "\"11\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.home.font.grow",
                Label = "글꼴 크기 크게",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","size_step":"grow"}""",
            },
            new CommandSpec
            {
                Id = "word.home.font.shrink",
                Label = "글꼴 크기 작게",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","size_step":"shrink"}""",
            },
            new CommandSpec
            {
                Id = "word.home.font.bold",
                Label = "굵게",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","bold":"{toggle}"}""",
                Pressed = "bold",
                Legacy = "bold",
            },
            new CommandSpec
            {
                Id = "word.home.font.italic",
                Label = "기울임꼴",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","italic":"{toggle}"}""",
                Pressed = "italic",
                Legacy = "italic",
            },
            new CommandSpec
            {
                Id = "word.home.font.underline",
                Label = "밑줄",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","underline":"{toggle ? single : none}"}""",
                Pressed = "underline",
                Legacy = "underline",
            },
            new CommandSpec
            {
                Id = "word.home.font.strike",
                Label = "취소선",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","strike":"{toggle ? single : none}"}""",
                Pressed = "strike",
            },
            new CommandSpec
            {
                Id = "word.home.font.color",
                Label = "글꼴 색",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","color":"{color}"}""",
                MenuStyle = "fontColor",
                ColorOwner = true,
                Dialog = new DialogSpec("글꼴 색", new[] { new DialogField("color", "색", "color", "\"C0392B\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.home.font.highlight",
                Label = "텍스트 강조 색",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","highlight":"{color}"}""",
                MenuStyle = "highlight",
                ColorOwner = true,
                Dialog = new DialogSpec("텍스트 강조 색", new[] { new DialogField("color", "색", "color", "\"FFE35A\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.home.para.bullets",
                Label = "글머리 기호",
                Class = RibbonCommandClass.Tool,
                Tool = "set_list",
                Args = """{"ids":"{ids}","type":"{toggle ? bullet : none}"}""",
                Pressed = "bullets",
                Legacy = "bullets",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "• 점", Args = """{"bullet":"•"}""" },
                    new CommandMenuSpec { Label = "○ 원", Args = """{"bullet":"○"}""" },
                    new CommandMenuSpec { Label = "■ 사각형", Args = """{"bullet":"■"}""" },
                    new CommandMenuSpec { Label = "□ 빈 사각형", Args = """{"bullet":"□"}""" },
                    new CommandMenuSpec { Label = "◆ 마름모", Args = """{"bullet":"◆"}""" },
                    new CommandMenuSpec { Label = "➢ 화살표", Args = """{"bullet":"➢"}""" },
                    new CommandMenuSpec { Label = "✓ 체크", Args = """{"bullet":"✓"}""" },
                    new CommandMenuSpec { Label = "– 줄표", Args = """{"bullet":"–"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.home.para.numbers",
                Label = "번호 매기기",
                Class = RibbonCommandClass.Tool,
                Tool = "set_list",
                Args = """{"ids":"{ids}","type":"{toggle ? number : none}"}""",
                Pressed = "numbers",
                Legacy = "numbers",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "1. 2. 3.", Args = """{"number_format":"decimal"}""" },
                    new CommandMenuSpec { Label = "1) 2) 3)", Args = """{"number_format":"decimal_paren"}""" },
                    new CommandMenuSpec { Label = "가. 나. 다.", Args = """{"number_format":"ganada"}""" },
                    new CommandMenuSpec { Label = "가) 나) 다)", Args = """{"number_format":"ganada_paren"}""" },
                    new CommandMenuSpec { Label = "① ② ③", Args = """{"number_format":"circled"}""" },
                    new CommandMenuSpec { Label = "I. II. III.", Args = """{"number_format":"upper_roman"}""" },
                    new CommandMenuSpec { Label = "A. B. C.", Args = """{"number_format":"upper_alpha"}""" },
                    new CommandMenuSpec { Label = "a. b. c.", Args = """{"number_format":"lower_alpha"}""" },
                    new CommandMenuSpec { Label = "번호 새로 시작", Args = """{"restart":true}""" },
                    new CommandMenuSpec { Label = "번호 값 정하기…", Args = """{"start_at":"{value}"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.home.para.multilevel",
                Label = "다단계 목록",
                Class = RibbonCommandClass.Tool,
                Tool = "set_list",
                Args = """{"ids":"{ids}","type":"multilevel","multilevel_preset":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "공문서 항목 (1. 가. 1) 가) (1) (가) ① ㉮)", Args = """{"multilevel_preset":"official"}""" },
                    new CommandMenuSpec { Label = "개요 (1. 가. 1) 가))", Args = """{"multilevel_preset":"outline"}""" },
                    new CommandMenuSpec { Label = "법조문 (1. 1.1 1.1.1)", Args = """{"multilevel_preset":"legal"}""" },
                    new CommandMenuSpec { Label = "기호 (■ □ ●)", Args = """{"multilevel_preset":"square"}""" },
                    new CommandMenuSpec { Label = "로마자 (I. A. 1.)", Args = """{"multilevel_preset":"roman"}""" },
                    new CommandMenuSpec { Label = "목록 없음", Args = """{"type":"none"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.home.para.outdent",
                Label = "내어쓰기",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","indent_step":-1}""",
            },
            new CommandSpec
            {
                Id = "word.home.para.indent",
                Label = "들여쓰기",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","indent_step":1}""",
            },
            new CommandSpec
            {
                Id = "word.home.para.alignleft",
                Label = "왼쪽 맞춤",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","align":"left"}""",
                Pressed = "alignLeft",
                Legacy = "alignLeft",
            },
            new CommandSpec
            {
                Id = "word.home.para.aligncenter",
                Label = "가운데 맞춤",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","align":"center"}""",
                Pressed = "alignCenter",
                Legacy = "alignCenter",
            },
            new CommandSpec
            {
                Id = "word.home.para.alignright",
                Label = "오른쪽 맞춤",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","align":"right"}""",
            },
            new CommandSpec
            {
                Id = "word.home.para.justify",
                Label = "양쪽 맞춤",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","align":"justify"}""",
            },
            new CommandSpec
            {
                Id = "word.home.para.spacing",
                Label = "줄 및 단락 간격",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","line_spacing":{"mode":"multiple","value":"{value}"}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "1.0", Args = """{"line_spacing":{"mode":"multiple","value":1.0}}""" },
                    new CommandMenuSpec { Label = "1.15", Args = """{"line_spacing":{"mode":"multiple","value":1.15}}""" },
                    new CommandMenuSpec { Label = "1.5", Args = """{"line_spacing":{"mode":"multiple","value":1.5}}""" },
                    new CommandMenuSpec { Label = "2.0", Args = """{"line_spacing":{"mode":"multiple","value":2.0}}""" },
                    new CommandMenuSpec { Label = "2.5", Args = """{"line_spacing":{"mode":"multiple","value":2.5}}""" },
                    new CommandMenuSpec { Label = "3.0", Args = """{"line_spacing":{"mode":"multiple","value":3.0}}""" },
                    new CommandMenuSpec { Label = "단락 앞에 간격 추가", Args = """{"space_before_pt":12}""" },
                    new CommandMenuSpec { Label = "단락 앞 간격 없앰", Args = """{"space_before_pt":0}""" },
                    new CommandMenuSpec { Label = "단락 뒤에 간격 추가", Args = """{"space_after_pt":8}""" },
                    new CommandMenuSpec { Label = "단락 뒤 간격 없앰", Args = """{"space_after_pt":0}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.home.styles.normal",
                Label = "표준",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","style":"Normal"}""",
                Pressed = "normal",
                Legacy = "normal",
            },
            new CommandSpec
            {
                Id = "word.home.styles.heading1",
                Label = "제목 1",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","style":"Heading 1"}""",
                Pressed = "heading1",
                Legacy = "heading1",
            },
            new CommandSpec
            {
                Id = "word.home.styles.heading2",
                Label = "제목 2",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","style":"Heading 2"}""",
                Pressed = "heading2",
                Legacy = "heading2",
            },
            new CommandSpec
            {
                Id = "word.home.styles.nospacing",
                Label = "간격 없음",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","style":"No Spacing"}""",
            },
            new CommandSpec
            {
                Id = "word.home.editing.find",
                Label = "찾기",
                Class = RibbonCommandClass.View,
                Action = "openNavigation",
                Args = """{"nav":true,"pane":"res","focus":"search"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "찾기", Args = """{"nav":true,"pane":"res","focus":"search"}""", Detail = "Ctrl+F. 탐색 창 [결과] 탭을 열고 검색 상자에 포커스" },
                    new CommandMenuSpec { Label = "고급 찾기…", Args = """{"command":"word.home.editing.findadv"}""", Detail = "대/소문자 구분, 단어 단위 옵션이 있는 찾기 대화상자" },
                    new CommandMenuSpec { Label = "이동…", Args = """{"command":"word.status.page"}""", Detail = "Ctrl+G. 쪽 번호로 가기" },
                },
            },
            new CommandSpec
            {
                Id = "word.home.editing.findadv",
                Label = "고급 찾기",
                Class = RibbonCommandClass.View,
                Action = "advancedFind",
            },
            new CommandSpec
            {
                Id = "word.home.editing.replace",
                Label = "바꾸기",
                Class = RibbonCommandClass.Tool,
                Tool = "replace_text",
                Args = """{"find":"{find}","replace":"{replace}","match_case":"{match_case}","whole_word":"{whole_word}","scope":"document"}""",
                Dialog = new DialogSpec("바꾸기", new[] { new DialogField("find", "찾을 말", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("replace", "바꿀 말", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("match_case", "대/소문자 구분", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("whole_word", "단어 단위로", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.insert.pages.cover",
                Label = "표지",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_page",
                Args = """{"kind":"cover_page","cover_page":{"design":"{value}","title":"{title}","subtitle":"{subtitle}","author":"{author}","date":"{date}"}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "표준", Args = """{"cover_page":{"design":"classic"}}""", Detail = "가운데에 큰 제목, 아래쪽에 작성자·날짜" },
                    new CommandMenuSpec { Label = "줄무늬", Args = """{"cover_page":{"design":"banded"}}""", Detail = "위쪽 색 띠 안에 제목" },
                    new CommandMenuSpec { Label = "간결", Args = """{"cover_page":{"design":"minimal"}}""", Detail = "왼쪽 맞춤, 가는 선" },
                    new CommandMenuSpec { Label = "굵은 제목", Args = """{"cover_page":{"design":"bold"}}""", Detail = "아주 큰 제목과 굵은 밑줄" },
                    new CommandMenuSpec { Label = "사이드 막대", Args = """{"cover_page":{"design":"sidebar"}}""", Detail = "왼쪽에 굵은 세로 막대" },
                    new CommandMenuSpec { Label = "현재 표지 삭제", Args = """{"cover_page":null,"remove":true}""", Detail = "문서 맨 앞 표지 블록을 지워요" },
                },
                Dialog = new DialogSpec("표지 정보", new[] { new DialogField("title", "제목", "text", "\"{문서 제목 속성 또는 첫 제목}\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("subtitle", "부제", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("author", "작성자", "text", "\"{문서 속성 작성자}\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("date", "날짜", "text", "\"{오늘 날짜: yyyy년 M월 d일}\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.insert.pages.blank",
                Label = "빈 페이지",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_page",
                Args = """{"kind":"blank_page","at":"{caret}"}""",
            },
            new CommandSpec
            {
                Id = "word.insert.pages.pagebreak",
                Label = "페이지 나누기",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_page",
                Args = """{"kind":"page_break","at":"{caret}"}""",
            },
            new CommandSpec
            {
                Id = "word.insert.table.table",
                Label = "표",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_table",
                Args = """{"after":"{id}","rows":"{rows × columns 빈 문자열}","header":true}""",
                MenuStyle = "table",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "칸 고르기 (10열 × 8행)", Args = """{"rows":"{hover rows}","columns":"{hover columns}"}""", Detail = "마우스를 올린 칸 수만큼 미리 보이고 누르면 넣어요" },
                    new CommandMenuSpec { Label = "표 삽입…", Args = """{"dialog":"insertTable"}""", Detail = "열·행 수를 직접 정해요" },
                },
                Dialog = new DialogSpec("표 삽입", new[] { new DialogField("columns", "열 수", "number", "3", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("rows", "행 수", "number", "3", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("header", "첫 줄은 제목 줄", "bool", "true", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("column_widths", "열 너비", "choice", "\"equal\"", new string[] { "equal" }, System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.insert.illus.picture",
                Label = "그림",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_object",
                Args = """{"action":"insert","kind":"picture","at":"{caret}","picture":{"path":"{file}","alt":"{alt}"}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "이 컴퓨터에서…", Args = """{"picker":"image"}""", Detail = "png, jpg, gif, bmp 파일을 골라요. 작업 폴더로 복사하지 않고 문서 안에 넣어요" },
                    new CommandMenuSpec { Label = "클립보드의 그림", Args = """{"picture":{"data_base64":"{clipboard image}"}}""", Detail = "화면 캡처 등을 Ctrl+V 하는 것과 같아요" },
                },
            },
            new CommandSpec
            {
                Id = "word.insert.illus.shapes",
                Label = "도형",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_object",
                Args = """{"action":"insert","kind":"shape","at":"{caret}","shape":{"preset":"{value}"}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "직선", Args = """{"shape":{"preset":"line"}}""" },
                    new CommandMenuSpec { Label = "화살표 선", Args = """{"shape":{"preset":"straightArrow"}}""" },
                    new CommandMenuSpec { Label = "직사각형", Args = """{"shape":{"preset":"rect"}}""" },
                    new CommandMenuSpec { Label = "둥근 직사각형", Args = """{"shape":{"preset":"roundRect"}}""" },
                    new CommandMenuSpec { Label = "타원", Args = """{"shape":{"preset":"ellipse"}}""" },
                    new CommandMenuSpec { Label = "삼각형", Args = """{"shape":{"preset":"triangle"}}""" },
                    new CommandMenuSpec { Label = "마름모", Args = """{"shape":{"preset":"diamond"}}""" },
                    new CommandMenuSpec { Label = "오각형", Args = """{"shape":{"preset":"pentagon"}}""" },
                    new CommandMenuSpec { Label = "육각형", Args = """{"shape":{"preset":"hexagon"}}""" },
                    new CommandMenuSpec { Label = "별", Args = """{"shape":{"preset":"star5"}}""" },
                    new CommandMenuSpec { Label = "오른쪽 화살표", Args = """{"shape":{"preset":"rightArrow"}}""" },
                    new CommandMenuSpec { Label = "왼쪽 화살표", Args = """{"shape":{"preset":"leftArrow"}}""" },
                    new CommandMenuSpec { Label = "위쪽 화살표", Args = """{"shape":{"preset":"upArrow"}}""" },
                    new CommandMenuSpec { Label = "아래쪽 화살표", Args = """{"shape":{"preset":"downArrow"}}""" },
                    new CommandMenuSpec { Label = "말풍선", Args = """{"shape":{"preset":"wedgeRoundRectCallout"}}""" },
                    new CommandMenuSpec { Label = "순서도: 처리", Args = """{"shape":{"preset":"flowChartProcess"}}""" },
                    new CommandMenuSpec { Label = "순서도: 판단", Args = """{"shape":{"preset":"flowChartDecision"}}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.insert.illus.chart",
                Label = "차트",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_object",
                Args = """{"action":"insert","kind":"chart","at":"{caret}","chart":{"type":"{value}","title":"{title}","from_table":"{table id or null}","categories":"{categories}","series":"{series}"}}""",
                Dialog = new DialogSpec("차트 넣기", new[] { new DialogField("type", "종류", "choice", "\"column\"", new string[] { "column", "bar", "line", "pie", "area", "scatter" }, new string[] { "세로 막대", "가로 막대", "꺾은선", "원", "영역", "분산형" }), new DialogField("title", "제목", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("data_source", "자료", "choice", "\"table\"", new string[] { "table", "sample" }, new string[] { "고른 표의 값", "예시 값" }), new DialogField("legend", "범례", "choice", "\"bottom\"", new string[] { "none", "bottom", "right" }, new string[] { "없음", "아래", "오른쪽" }) }),
            },
            new CommandSpec
            {
                Id = "word.insert.links.link",
                Label = "링크",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_reference",
                Args = """{"kind":"hyperlink","at":"{targets[0]}","hyperlink":{"url":"{url}","text":"{text}"}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "링크 삽입…", Args = """{"dialog":"hyperlink"}""", Detail = "표시할 글자와 주소(https, mailto)를 정해요" },
                    new CommandMenuSpec { Label = "책갈피…", Args = """{"kind":"bookmark","bookmark":{"name":"{name}"}}""", Detail = "고른 글자에 이름표를 달아요" },
                    new CommandMenuSpec { Label = "링크 제거", Args = """{"action":"delete","kind":"hyperlink"}""", Detail = "링크만 없애고 글자는 그대로" },
                },
                Dialog = new DialogSpec("링크 삽입", new[] { new DialogField("text", "표시할 글자", "text", "\"{고른 글자}\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("url", "주소", "text", "\"https://\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("tooltip", "풍선 도움말", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.insert.comments.new",
                Label = "메모",
                Class = RibbonCommandClass.Tool,
                Tool = "manage_comments",
                Args = """{"action":"add","target":"{targets[0]}","text":"{text}","author":"{author}"}""",
                Dialog = new DialogSpec("새 메모", new[] { new DialogField("text", "메모 내용", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.insert.hf.header",
                Label = "머리글",
                Class = RibbonCommandClass.Tool,
                Tool = "set_header_footer",
                Args = """{"part":"header","section":"all","preset":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "비어 있음", Args = """{"preset":"blank"}""" },
                    new CommandMenuSpec { Label = "단순 (왼쪽)", Args = """{"preset":"left"}""" },
                    new CommandMenuSpec { Label = "단순 (가운데)", Args = """{"preset":"center"}""" },
                    new CommandMenuSpec { Label = "단순 (오른쪽)", Args = """{"preset":"right"}""" },
                    new CommandMenuSpec { Label = "3단 (왼쪽·가운데·오른쪽)", Args = """{"preset":"three_column"}""" },
                    new CommandMenuSpec { Label = "머리글 편집…", Args = """{"dialog":"headerEdit"}""", Detail = "왼쪽/가운데/오른쪽 글을 직접 써요. 쪽 번호·날짜는 {쪽} {전체쪽} {날짜}로" },
                    new CommandMenuSpec { Label = "첫 페이지 다르게", Args = """{"different_first_page":"{toggle}"}""" },
                    new CommandMenuSpec { Label = "홀수/짝수 페이지 다르게", Args = """{"different_odd_even":"{toggle}"}""" },
                    new CommandMenuSpec { Label = "머리글 제거", Args = """{"remove":true}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.insert.hf.footer",
                Label = "바닥글",
                Class = RibbonCommandClass.Tool,
                Tool = "set_header_footer",
                Args = """{"part":"footer","section":"all","preset":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "비어 있음", Args = """{"preset":"blank"}""" },
                    new CommandMenuSpec { Label = "단순 (가운데 쪽 번호)", Args = """{"preset":"center_page"}""" },
                    new CommandMenuSpec { Label = "쪽 번호 ‘1 / 3’", Args = """{"preset":"page_x_of_y"}""" },
                    new CommandMenuSpec { Label = "3단", Args = """{"preset":"three_column"}""" },
                    new CommandMenuSpec { Label = "바닥글 편집…", Args = """{"dialog":"footerEdit"}""" },
                    new CommandMenuSpec { Label = "바닥글 제거", Args = """{"remove":true}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.insert.hf.pagenumber",
                Label = "페이지 번호",
                Class = RibbonCommandClass.Tool,
                Tool = "set_header_footer",
                Args = """{"page_number":{"position":"{top|bottom|current}","align":"{left|center|right|outside|inside}","style":"{plain|page_x_of_y|dash|brackets}"}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "페이지 위쪽 · 왼쪽", Args = """{"page_number":{"position":"top","align":"left"}}""" },
                    new CommandMenuSpec { Label = "페이지 위쪽 · 가운데", Args = """{"page_number":{"position":"top","align":"center"}}""" },
                    new CommandMenuSpec { Label = "페이지 위쪽 · 오른쪽", Args = """{"page_number":{"position":"top","align":"right"}}""" },
                    new CommandMenuSpec { Label = "페이지 아래쪽 · 왼쪽", Args = """{"page_number":{"position":"bottom","align":"left"}}""" },
                    new CommandMenuSpec { Label = "페이지 아래쪽 · 가운데", Args = """{"page_number":{"position":"bottom","align":"center"}}""" },
                    new CommandMenuSpec { Label = "페이지 아래쪽 · 오른쪽", Args = """{"page_number":{"position":"bottom","align":"right"}}""" },
                    new CommandMenuSpec { Label = "현재 위치", Args = """{"page_number":{"position":"current"}}""" },
                    new CommandMenuSpec { Label = "페이지 번호 서식…", Args = """{"dialog":"pageNumberFormat"}""", Detail = "1,2,3 / I,II,III / i,ii,iii / A,B,C / a,b,c, 시작 번호, '1 / 3' 꼴" },
                    new CommandMenuSpec { Label = "페이지 번호 제거", Args = """{"page_number":{"position":"none"}}""" },
                },
                Dialog = new DialogSpec("페이지 번호 서식", new[] { new DialogField("format", "번호 모양", "choice", "\"decimal\"", new string[] { "decimal", "upper_roman", "lower_roman", "upper_alpha", "lower_alpha" }, System.Array.Empty<string>()), new DialogField("style", "꼴", "choice", "\"plain\"", new string[] { "plain", "page_x_of_y", "dash", "brackets" }, new string[] { "1", "1 / 3", "- 1 -", "[1]" }), new DialogField("start_at", "시작 번호", "number", "1", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.insert.text.textbox",
                Label = "텍스트 상자",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_object",
                Args = """{"action":"insert","kind":"textbox","at":"{caret}","shape":{"text":"{text}","preset":"rect"}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "간단한 텍스트 상자", Args = """{"shape":{"text":"여기에 글을 쓰세요","preset":"rect"}}""" },
                    new CommandMenuSpec { Label = "세로 텍스트 상자", Args = """{"shape":{"text":"여기에 글을 쓰세요","preset":"rect","text_direction":"vertical"}}""", Detail = "위에서 아래로 쓰는 글" },
                },
            },
            new CommandSpec
            {
                Id = "word.insert.text.datetime",
                Label = "날짜 및 시간",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_field",
                Args = """{"action":"insert","kind":"date_time","at":"{caret}","format":"{value}","update_automatically":"{auto}"}""",
                Dialog = new DialogSpec("날짜 및 시간", new[] { new DialogField("format", "모양", "choice", "\"yyyy. M. d.\"", new string[] { "yyyy. M. d.", "yyyy년 M월 d일", "yyyy-MM-dd", "M월 d일 dddd", "yyyy년 M월 d일 dddd", "tt h:mm", "HH:mm", "yyyy년 M월 d일 tt h:mm" }, System.Array.Empty<string>()), new DialogField("update_automatically", "자동으로 업데이트", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.insert.text.symbol",
                Label = "기호",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_text",
                Args = """{"ops":[{"op":"insert","id":"{id}","offset":"{offset}","text":"{value}"}]}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "©", Args = """{"value":"©"}""" },
                    new CommandMenuSpec { Label = "®", Args = """{"value":"®"}""" },
                    new CommandMenuSpec { Label = "™", Args = """{"value":"™"}""" },
                    new CommandMenuSpec { Label = "§", Args = """{"value":"§"}""" },
                    new CommandMenuSpec { Label = "°", Args = """{"value":"°"}""" },
                    new CommandMenuSpec { Label = "±", Args = """{"value":"±"}""" },
                    new CommandMenuSpec { Label = "×", Args = """{"value":"×"}""" },
                    new CommandMenuSpec { Label = "÷", Args = """{"value":"÷"}""" },
                    new CommandMenuSpec { Label = "※", Args = """{"value":"※"}""" },
                    new CommandMenuSpec { Label = "★", Args = """{"value":"★"}""" },
                    new CommandMenuSpec { Label = "☆", Args = """{"value":"☆"}""" },
                    new CommandMenuSpec { Label = "○", Args = """{"value":"○"}""" },
                    new CommandMenuSpec { Label = "●", Args = """{"value":"●"}""" },
                    new CommandMenuSpec { Label = "□", Args = """{"value":"□"}""" },
                    new CommandMenuSpec { Label = "■", Args = """{"value":"■"}""" },
                    new CommandMenuSpec { Label = "△", Args = """{"value":"△"}""" },
                    new CommandMenuSpec { Label = "▲", Args = """{"value":"▲"}""" },
                    new CommandMenuSpec { Label = "▽", Args = """{"value":"▽"}""" },
                    new CommandMenuSpec { Label = "▼", Args = """{"value":"▼"}""" },
                    new CommandMenuSpec { Label = "◇", Args = """{"value":"◇"}""" },
                    new CommandMenuSpec { Label = "◆", Args = """{"value":"◆"}""" },
                    new CommandMenuSpec { Label = "→", Args = """{"value":"→"}""" },
                    new CommandMenuSpec { Label = "←", Args = """{"value":"←"}""" },
                    new CommandMenuSpec { Label = "↑", Args = """{"value":"↑"}""" },
                    new CommandMenuSpec { Label = "↓", Args = """{"value":"↓"}""" },
                    new CommandMenuSpec { Label = "↔", Args = """{"value":"↔"}""" },
                    new CommandMenuSpec { Label = "≠", Args = """{"value":"≠"}""" },
                    new CommandMenuSpec { Label = "≤", Args = """{"value":"≤"}""" },
                    new CommandMenuSpec { Label = "≥", Args = """{"value":"≥"}""" },
                    new CommandMenuSpec { Label = "∞", Args = """{"value":"∞"}""" },
                    new CommandMenuSpec { Label = "₩", Args = """{"value":"₩"}""" },
                    new CommandMenuSpec { Label = "€", Args = """{"value":"€"}""" },
                    new CommandMenuSpec { Label = "¥", Args = """{"value":"¥"}""" },
                    new CommandMenuSpec { Label = "①", Args = """{"value":"①"}""" },
                    new CommandMenuSpec { Label = "②", Args = """{"value":"②"}""" },
                    new CommandMenuSpec { Label = "③", Args = """{"value":"③"}""" },
                    new CommandMenuSpec { Label = "④", Args = """{"value":"④"}""" },
                    new CommandMenuSpec { Label = "⑤", Args = """{"value":"⑤"}""" },
                    new CommandMenuSpec { Label = "⑥", Args = """{"value":"⑥"}""" },
                    new CommandMenuSpec { Label = "⑦", Args = """{"value":"⑦"}""" },
                    new CommandMenuSpec { Label = "⑧", Args = """{"value":"⑧"}""" },
                    new CommandMenuSpec { Label = "⑨", Args = """{"value":"⑨"}""" },
                    new CommandMenuSpec { Label = "⑩", Args = """{"value":"⑩"}""" },
                    new CommandMenuSpec { Label = "㉠", Args = """{"value":"㉠"}""" },
                    new CommandMenuSpec { Label = "㉡", Args = """{"value":"㉡"}""" },
                    new CommandMenuSpec { Label = "㉢", Args = """{"value":"㉢"}""" },
                    new CommandMenuSpec { Label = "㉮", Args = """{"value":"㉮"}""" },
                    new CommandMenuSpec { Label = "㉯", Args = """{"value":"㉯"}""" },
                    new CommandMenuSpec { Label = "㉰", Args = """{"value":"㉰"}""" },
                    new CommandMenuSpec { Label = "기타 기호…", Args = """{"dialog":"symbols"}""", Detail = "유니코드 번호(예: 2605)나 목록에서 골라요" },
                },
            },
            new CommandSpec
            {
                Id = "word.design.format.theme",
                Label = "테마",
                Class = RibbonCommandClass.Tool,
                Tool = "set_document_design",
                Args = """{"theme":"{value}","apply_to_styles":true}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "Office", Args = """{"theme":"office"}""", Detail = "맑은 고딕 · 파랑 계열 (새 문서 기본)" },
                    new CommandMenuSpec { Label = "모던", Args = """{"theme":"modern"}""", Detail = "맑은 고딕 · 앱 색(파랑·초록·주황·청록)" },
                    new CommandMenuSpec { Label = "클래식", Args = """{"theme":"classic"}""", Detail = "바탕 · 차분한 남색·갈색" },
                    new CommandMenuSpec { Label = "차분한", Args = """{"theme":"calm"}""", Detail = "맑은 고딕 · 회청색 계열" },
                    new CommandMenuSpec { Label = "선명한", Args = """{"theme":"vivid"}""", Detail = "맑은 고딕 · 또렷한 원색" },
                    new CommandMenuSpec { Label = "흑백", Args = """{"theme":"mono"}""", Detail = "맑은 고딕 · 회색조(인쇄용)" },
                },
            },
            new CommandSpec
            {
                Id = "word.design.format.colors",
                Label = "색",
                Class = RibbonCommandClass.Tool,
                Tool = "set_document_design",
                Args = """{"colors":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "Office", Args = """{"colors":"office"}""" },
                    new CommandMenuSpec { Label = "청록", Args = """{"colors":"teal"}""" },
                    new CommandMenuSpec { Label = "보라", Args = """{"colors":"violet"}""" },
                    new CommandMenuSpec { Label = "주황", Args = """{"colors":"orange"}""" },
                    new CommandMenuSpec { Label = "초록", Args = """{"colors":"green"}""" },
                    new CommandMenuSpec { Label = "회색조", Args = """{"colors":"grayscale"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.design.format.fonts",
                Label = "글꼴",
                Class = RibbonCommandClass.Tool,
                Tool = "set_document_design",
                Args = """{"fonts":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "맑은 고딕", Args = """{"fonts":{"heading":"맑은 고딕","body":"맑은 고딕"}}""" },
                    new CommandMenuSpec { Label = "맑은 고딕 · 바탕", Args = """{"fonts":{"heading":"맑은 고딕","body":"바탕"}}""", Detail = "제목은 맑은 고딕, 본문은 바탕" },
                    new CommandMenuSpec { Label = "바탕", Args = """{"fonts":{"heading":"바탕","body":"바탕"}}""" },
                    new CommandMenuSpec { Label = "돋움 · 바탕", Args = """{"fonts":{"heading":"돋움","body":"바탕"}}""" },
                    new CommandMenuSpec { Label = "Arial · 맑은 고딕", Args = """{"fonts":{"heading":"Arial","body":"Arial","heading_east_asian":"맑은 고딕","body_east_asian":"맑은 고딕"}}""" },
                    new CommandMenuSpec { Label = "Times New Roman · 바탕", Args = """{"fonts":{"heading":"Times New Roman","body":"Times New Roman","heading_east_asian":"바탕","body_east_asian":"바탕"}}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.design.format.spacing",
                Label = "단락 간격",
                Class = RibbonCommandClass.Tool,
                Tool = "set_document_design",
                Args = """{"paragraph_spacing":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "기본", Args = """{"paragraph_spacing":"default"}""", Detail = "뒤 8pt · 줄 1.08" },
                    new CommandMenuSpec { Label = "단락 간격 없음", Args = """{"paragraph_spacing":"none"}""", Detail = "뒤 0pt · 줄 1.0" },
                    new CommandMenuSpec { Label = "촘촘하게", Args = """{"paragraph_spacing":"compact"}""", Detail = "뒤 4pt · 줄 1.0" },
                    new CommandMenuSpec { Label = "좁게", Args = """{"paragraph_spacing":"tight"}""", Detail = "뒤 6pt · 줄 1.15" },
                    new CommandMenuSpec { Label = "열기", Args = """{"paragraph_spacing":"open"}""", Detail = "뒤 10pt · 줄 1.15" },
                    new CommandMenuSpec { Label = "느슨하게", Args = """{"paragraph_spacing":"relaxed"}""", Detail = "뒤 6pt · 줄 1.5" },
                    new CommandMenuSpec { Label = "2배", Args = """{"paragraph_spacing":"double"}""", Detail = "뒤 8pt · 줄 2.0" },
                },
            },
            new CommandSpec
            {
                Id = "word.design.format.default",
                Label = "기본값으로 설정",
                Class = RibbonCommandClass.File,
                Action = "saveAsNewFileDefault",
            },
            new CommandSpec
            {
                Id = "word.design.bg.watermark",
                Label = "워터마크",
                Class = RibbonCommandClass.Tool,
                Tool = "set_header_footer",
                Args = """{"section":"all","watermark":{"action":"add","text":"{value}","layout":"diagonal","semi_transparent":true}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "기밀", Args = """{"watermark":{"action":"add","text":"기밀"}}""" },
                    new CommandMenuSpec { Label = "초안", Args = """{"watermark":{"action":"add","text":"초안"}}""" },
                    new CommandMenuSpec { Label = "사본", Args = """{"watermark":{"action":"add","text":"사본"}}""" },
                    new CommandMenuSpec { Label = "긴급", Args = """{"watermark":{"action":"add","text":"긴급"}}""" },
                    new CommandMenuSpec { Label = "견본", Args = """{"watermark":{"action":"add","text":"견본"}}""" },
                    new CommandMenuSpec { Label = "사용자 지정 워터마크…", Args = """{"dialog":"watermark"}""", Detail = "글, 글꼴, 크기, 색, 반투명, 대각선/가로" },
                    new CommandMenuSpec { Label = "워터마크 제거", Args = """{"watermark":{"action":"remove"}}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.design.bg.pagecolor",
                Label = "페이지 색",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"page_color":"{color}"}""",
                MenuStyle = "pageColor",
                Dialog = new DialogSpec("색 고르기", new[] { new DialogField("color", "색", "color", "\"FFFFFF\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.design.bg.pageborder",
                Label = "페이지 테두리",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"page_borders":{"style":"{box|shadow|3d|none}","line":"{single|double|dashed|dotted}","width_pt":"{value}","color":"{color}","offset_from":"page"}}""",
                Dialog = new DialogSpec("페이지 테두리", new[] { new DialogField("style", "설정", "choice", "\"none\"", new string[] { "none", "box", "shadow", "3d" }, new string[] { "없음", "상자", "그림자", "3차원" }), new DialogField("line", "선 모양", "choice", "\"single\"", new string[] { "single", "double", "dashed", "dotted" }, new string[] { "실선", "이중선", "파선", "점선" }), new DialogField("width_pt", "두께(pt)", "number", "0.75", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("color", "색", "color", "\"000000\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("offset_from", "테두리 위치", "choice", "\"text\"", new string[] { "text", "page" }, new string[] { "본문에서", "용지 가장자리에서" }), new DialogField("apply_to", "적용 대상", "choice", "\"document\"", new string[] { "document", "section" }, new string[] { "문서 전체", "이 구역" }) }),
            },
            new CommandSpec
            {
                Id = "word.layout.setup.margins",
                Label = "여백",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"scope":"document","margins":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "보통", Args = """{"margins":"normal"}""", Detail = "위·아래·왼쪽·오른쪽 2.54cm" },
                    new CommandMenuSpec { Label = "좁게", Args = """{"margins":"narrow"}""", Detail = "1.27cm" },
                    new CommandMenuSpec { Label = "중간", Args = """{"margins":"moderate"}""", Detail = "위·아래 2.54cm, 좌우 1.91cm" },
                    new CommandMenuSpec { Label = "넓게", Args = """{"margins":"wide"}""", Detail = "위·아래 2.54cm, 좌우 5.08cm" },
                    new CommandMenuSpec { Label = "마주 보기", Args = """{"margins":"mirrored","mirror_margins":true}""", Detail = "안쪽 3.18cm, 바깥쪽 2.54cm" },
                    new CommandMenuSpec { Label = "공문서", Args = """{"margins":"official"}""", Detail = "위 3.0cm · 아래 1.5cm · 좌우 2.0cm" },
                    new CommandMenuSpec { Label = "사용자 지정 여백…", Args = """{"dialog":"margins"}""", Detail = "위·아래·왼쪽·오른쪽·제본·머리글·바닥글(cm)" },
                },
                Dialog = new DialogSpec("여백", new[] { new DialogField("top_cm", "위쪽", "number", "3.0", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("bottom_cm", "아래쪽", "number", "2.54", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("left_cm", "왼쪽", "number", "2.54", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("right_cm", "오른쪽", "number", "2.54", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("gutter_cm", "제본 여백", "number", "0", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("header_cm", "머리글 위치", "number", "1.5", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("footer_cm", "바닥글 위치", "number", "1.75", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("scope", "적용 대상", "choice", "\"document\"", new string[] { "document", "section", "from_here" }, new string[] { "문서 전체", "이 구역", "여기부터" }) }),
            },
            new CommandSpec
            {
                Id = "word.layout.setup.orientation",
                Label = "용지 방향",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"scope":"document","orientation":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "세로", Args = """{"orientation":"portrait"}""" },
                    new CommandMenuSpec { Label = "가로", Args = """{"orientation":"landscape"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.layout.setup.size",
                Label = "크기",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"scope":"document","size":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "A4 (21 × 29.7cm)", Args = """{"size":"A4"}""" },
                    new CommandMenuSpec { Label = "A3 (29.7 × 42cm)", Args = """{"size":"A3"}""" },
                    new CommandMenuSpec { Label = "A5 (14.8 × 21cm)", Args = """{"size":"A5"}""" },
                    new CommandMenuSpec { Label = "B4 (25.7 × 36.4cm)", Args = """{"size":"B4"}""" },
                    new CommandMenuSpec { Label = "B5 (18.2 × 25.7cm)", Args = """{"size":"B5"}""" },
                    new CommandMenuSpec { Label = "레터 (21.59 × 27.94cm)", Args = """{"size":"Letter"}""" },
                    new CommandMenuSpec { Label = "리갈 (21.59 × 35.56cm)", Args = """{"size":"Legal"}""" },
                    new CommandMenuSpec { Label = "타블로이드 (27.94 × 43.18cm)", Args = """{"size":"Tabloid"}""" },
                    new CommandMenuSpec { Label = "Executive (18.41 × 26.67cm)", Args = """{"size":"Executive"}""" },
                    new CommandMenuSpec { Label = "사용자 지정 크기…", Args = """{"dialog":"paperSize"}""", Detail = "너비·높이(cm)" },
                },
                Dialog = new DialogSpec("용지 크기", new[] { new DialogField("width_cm", "너비", "number", "21.0", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("height_cm", "높이", "number", "29.7", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("scope", "적용 대상", "choice", "\"document\"", new string[] { "document", "section", "from_here" }, new string[] { "문서 전체", "이 구역", "여기부터" }) }),
            },
            new CommandSpec
            {
                Id = "word.layout.setup.columns",
                Label = "단",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"scope":"document","columns":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "하나", Args = """{"columns":{"preset":"one"}}""" },
                    new CommandMenuSpec { Label = "둘", Args = """{"columns":{"preset":"two"}}""" },
                    new CommandMenuSpec { Label = "셋", Args = """{"columns":{"preset":"three"}}""" },
                    new CommandMenuSpec { Label = "왼쪽", Args = """{"columns":{"preset":"left"}}""", Detail = "왼쪽 단이 좁아요" },
                    new CommandMenuSpec { Label = "오른쪽", Args = """{"columns":{"preset":"right"}}""", Detail = "오른쪽 단이 좁아요" },
                    new CommandMenuSpec { Label = "사용자 지정 단…", Args = """{"dialog":"columns"}""", Detail = "단 수, 간격, 구분선" },
                },
                Dialog = new DialogSpec("단", new[] { new DialogField("count", "단 수", "number", "2", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("space_cm", "간격(cm)", "number", "1.25", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("line_between", "구분선", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("equal", "너비 같게", "bool", "true", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("scope", "적용 대상", "choice", "\"document\"", new string[] { "document", "section", "from_here" }, new string[] { "문서 전체", "이 구역", "여기부터" }) }),
            },
            new CommandSpec
            {
                Id = "word.layout.setup.breaks",
                Label = "나누기",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_page",
                Args = """{"kind":"{value}","at":"{caret}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "페이지 나누기", Args = """{"kind":"page_break"}""" },
                    new CommandMenuSpec { Label = "단 나누기", Args = """{"kind":"column_break"}""", Detail = "단이 둘 이상일 때 다음 단으로" },
                    new CommandMenuSpec { Label = "텍스트 줄 바꿈", Args = """{"kind":"text_wrapping_break"}""" },
                    new CommandMenuSpec { Label = "구역 나누기: 다음 페이지", Args = """{"kind":"section_next_page"}""" },
                    new CommandMenuSpec { Label = "구역 나누기: 현재 페이지", Args = """{"kind":"section_continuous"}""" },
                    new CommandMenuSpec { Label = "구역 나누기: 짝수 페이지", Args = """{"kind":"section_even_page"}""" },
                    new CommandMenuSpec { Label = "구역 나누기: 홀수 페이지", Args = """{"kind":"section_odd_page"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.layout.setup.linenumbers",
                Label = "줄 번호",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"scope":"document","line_numbers":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "없음", Args = """{"line_numbers":{"mode":"none"}}""" },
                    new CommandMenuSpec { Label = "연속", Args = """{"line_numbers":{"mode":"continuous"}}""" },
                    new CommandMenuSpec { Label = "페이지마다 다시 시작", Args = """{"line_numbers":{"mode":"restart_page"}}""" },
                    new CommandMenuSpec { Label = "구역마다 다시 시작", Args = """{"line_numbers":{"mode":"restart_section"}}""" },
                    new CommandMenuSpec { Label = "현재 단락에서 사용 안 함", Args = """{"command":"word.layout.setup.linenumbers.suppress"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.layout.para.indentleft",
                Label = "왼쪽 들여쓰기",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","indent_left_cm":"{value}"}""",
                MenuStyle = "input",
                Unit = "cm",
                Dialog = new DialogSpec("왼쪽 들여쓰기", new[] { new DialogField("value", "왼쪽 들여쓰기", "number", "0", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.layout.para.indentright",
                Label = "오른쪽 들여쓰기",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","indent_right_cm":"{value}"}""",
                MenuStyle = "input",
                Unit = "cm",
                Dialog = new DialogSpec("오른쪽 들여쓰기", new[] { new DialogField("value", "오른쪽 들여쓰기", "number", "0", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.layout.para.spacebefore",
                Label = "단락 앞 간격",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","space_before_pt":"{value}"}""",
                MenuStyle = "input",
                Unit = "pt",
                Dialog = new DialogSpec("단락 앞 간격", new[] { new DialogField("value", "단락 앞 간격", "number", "0", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.layout.para.spaceafter",
                Label = "단락 뒤 간격",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","space_after_pt":"{value}"}""",
                MenuStyle = "input",
                Unit = "pt",
                Dialog = new DialogSpec("단락 뒤 간격", new[] { new DialogField("value", "단락 뒤 간격", "number", "0", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.layout.arrange.position",
                Label = "위치",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_object",
                Args = """{"action":"format","id":"{object}","layout":{"wrap":"square","horizontal":"{h}","vertical":"{v}"}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "본문 위치에서 글자처럼 취급", Args = """{"layout":{"wrap":"inline"}}""" },
                    new CommandMenuSpec { Label = "위쪽 왼쪽", Args = """{"layout":{"wrap":"square","horizontal":"left","vertical":"top"}}""" },
                    new CommandMenuSpec { Label = "위쪽 가운데", Args = """{"layout":{"wrap":"square","horizontal":"center","vertical":"top"}}""" },
                    new CommandMenuSpec { Label = "위쪽 오른쪽", Args = """{"layout":{"wrap":"square","horizontal":"right","vertical":"top"}}""" },
                    new CommandMenuSpec { Label = "가운데 왼쪽", Args = """{"layout":{"wrap":"square","horizontal":"left","vertical":"center"}}""" },
                    new CommandMenuSpec { Label = "가운데", Args = """{"layout":{"wrap":"square","horizontal":"center","vertical":"center"}}""" },
                    new CommandMenuSpec { Label = "가운데 오른쪽", Args = """{"layout":{"wrap":"square","horizontal":"right","vertical":"center"}}""" },
                    new CommandMenuSpec { Label = "아래쪽 왼쪽", Args = """{"layout":{"wrap":"square","horizontal":"left","vertical":"bottom"}}""" },
                    new CommandMenuSpec { Label = "아래쪽 가운데", Args = """{"layout":{"wrap":"square","horizontal":"center","vertical":"bottom"}}""" },
                    new CommandMenuSpec { Label = "아래쪽 오른쪽", Args = """{"layout":{"wrap":"square","horizontal":"right","vertical":"bottom"}}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.layout.arrange.wrap",
                Label = "텍스트 줄 바꿈",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_object",
                Args = """{"action":"format","id":"{object}","layout":{"wrap":"{value}"}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "글자처럼 취급", Args = """{"layout":{"wrap":"inline"}}""" },
                    new CommandMenuSpec { Label = "정사각형", Args = """{"layout":{"wrap":"square"}}""" },
                    new CommandMenuSpec { Label = "빽빽하게", Args = """{"layout":{"wrap":"tight"}}""" },
                    new CommandMenuSpec { Label = "투과", Args = """{"layout":{"wrap":"through"}}""" },
                    new CommandMenuSpec { Label = "위아래", Args = """{"layout":{"wrap":"top_bottom"}}""" },
                    new CommandMenuSpec { Label = "텍스트 뒤로", Args = """{"layout":{"wrap":"behind"}}""" },
                    new CommandMenuSpec { Label = "텍스트 앞으로", Args = """{"layout":{"wrap":"in_front"}}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.layout.arrange.front",
                Label = "앞으로 가져오기",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_object",
                Args = """{"action":"format","id":"{object}","z_order":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "앞으로 가져오기", Args = """{"z_order":"bring_forward"}""" },
                    new CommandMenuSpec { Label = "맨 앞으로 가져오기", Args = """{"z_order":"bring_to_front"}""" },
                    new CommandMenuSpec { Label = "텍스트 앞으로 가져오기", Args = """{"z_order":"in_front_of_text"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.layout.arrange.back",
                Label = "뒤로 보내기",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_object",
                Args = """{"action":"format","id":"{object}","z_order":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "뒤로 보내기", Args = """{"z_order":"send_backward"}""" },
                    new CommandMenuSpec { Label = "맨 뒤로 보내기", Args = """{"z_order":"send_to_back"}""" },
                    new CommandMenuSpec { Label = "텍스트 뒤로 보내기", Args = """{"z_order":"behind_text"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.refs.toc.toc",
                Label = "목차",
                Class = RibbonCommandClass.Tool,
                Tool = "manage_toc",
                Args = """{"action":"insert","kind":"contents","at":"{caret}","contents":{"style":"{value}","levels":{"from":1,"to":3}}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "자동 목차 1 (‘목차’)", Args = """{"contents":{"style":"automatic1","title":"목차"}}""", Detail = "제목 1~3, 점선과 쪽 번호, 누르면 이동" },
                    new CommandMenuSpec { Label = "자동 목차 2 (‘내용’)", Args = """{"contents":{"style":"automatic2","title":"내용"}}""" },
                    new CommandMenuSpec { Label = "수동 목차", Args = """{"contents":{"style":"manual"}}""", Detail = "글자만 있는 틀. 쪽 번호를 직접 고쳐요" },
                    new CommandMenuSpec { Label = "사용자 지정 목차…", Args = """{"dialog":"tocOptions"}""", Detail = "단계 범위, 쪽 번호 표시, 탭 채움선" },
                    new CommandMenuSpec { Label = "목차 제거", Args = """{"action":"remove"}""" },
                },
                Dialog = new DialogSpec("목차", new[] { new DialogField("from", "처음 단계", "number", "1", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("to", "마지막 단계", "number", "3", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("page_numbers", "쪽 번호 표시", "bool", "true", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("right_align", "쪽 번호 오른쪽 맞춤", "bool", "true", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("tab_leader", "채움선", "choice", "\"dot\"", new string[] { "dot", "hyphen", "underline", "none" }, new string[] { "점선", "하이픈", "밑줄", "없음" }), new DialogField("hyperlinks", "누르면 이동", "bool", "true", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.refs.toc.addtext",
                Label = "텍스트 추가",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","outline_level":"{value}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "목차에 표시 안 함", Args = """{"outline_level":0}""" },
                    new CommandMenuSpec { Label = "수준 1", Args = """{"outline_level":1}""" },
                    new CommandMenuSpec { Label = "수준 2", Args = """{"outline_level":2}""" },
                    new CommandMenuSpec { Label = "수준 3", Args = """{"outline_level":3}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.refs.toc.update",
                Label = "목차 업데이트",
                Class = RibbonCommandClass.Tool,
                Tool = "manage_toc",
                Args = """{"action":"update","kind":"contents"}""",
            },
            new CommandSpec
            {
                Id = "word.refs.notes.footnote",
                Label = "각주 삽입",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_reference",
                Args = """{"action":"insert","kind":"footnote","at":"{caret}","note":{"text":"{text}"}}""",
                Dialog = new DialogSpec("각주", new[] { new DialogField("text", "각주 내용", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.refs.notes.endnote",
                Label = "미주 삽입",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_reference",
                Args = """{"action":"insert","kind":"endnote","at":"{caret}","note":{"text":"{text}"}}""",
                Dialog = new DialogSpec("미주", new[] { new DialogField("text", "미주 내용", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.refs.notes.next",
                Label = "다음 각주",
                Class = RibbonCommandClass.View,
                Action = "goToNote",
                Args = """{"direction":"{next|prev}","kind":"{footnote|endnote}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "다음 각주", Args = """{"direction":"next","kind":"footnote"}""" },
                    new CommandMenuSpec { Label = "이전 각주", Args = """{"direction":"prev","kind":"footnote"}""" },
                    new CommandMenuSpec { Label = "다음 미주", Args = """{"direction":"next","kind":"endnote"}""" },
                    new CommandMenuSpec { Label = "이전 미주", Args = """{"direction":"prev","kind":"endnote"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.refs.captions.insert",
                Label = "캡션 삽입",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_reference",
                Args = """{"action":"insert","kind":"caption","caption":{"label":"{value}","text":"{text}","position":"{above|below}","target":"{table or object host id}"}}""",
                Dialog = new DialogSpec("캡션", new[] { new DialogField("label", "레이블", "choice", "\"표\"", new string[] { "표", "그림", "수식" }, System.Array.Empty<string>()), new DialogField("text", "캡션 내용", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("position", "위치", "choice", "\"above\"", new string[] { "above", "below" }, new string[] { "선택 항목 위", "선택 항목 아래" }), new DialogField("include_label", "번호 붙이기", "bool", "true", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.refs.captions.tof",
                Label = "그림 목차 삽입",
                Class = RibbonCommandClass.Tool,
                Tool = "manage_toc",
                Args = """{"action":"insert","kind":"figures","at":"{caret}","figures":{"label":"{value}"}}""",
                Dialog = new DialogSpec("그림 목차", new[] { new DialogField("label", "캡션 레이블", "choice", "\"그림\"", new string[] { "그림", "표", "수식" }, System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.refs.captions.crossref",
                Label = "상호 참조",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_reference",
                Args = """{"action":"insert","kind":"cross_reference","at":"{caret}","cross_reference":{"type":"{value}","target":"{target}","show":"{show}","as_link":true}}""",
                Dialog = new DialogSpec("상호 참조", new[] { new DialogField("type", "참조 대상", "choice", "\"caption_table\"", new string[] { "caption_table", "caption_figure", "caption_equation", "heading", "bookmark", "footnote", "endnote" }, new string[] { "표", "그림", "수식", "제목", "책갈피", "각주", "미주" }), new DialogField("show", "삽입할 내용", "choice", "\"label_and_number\"", new string[] { "label_and_number", "only_label", "number", "text", "page_number", "above_below" }, new string[] { "레이블 및 번호", "레이블만", "번호만", "텍스트", "쪽 번호", "위/아래" }), new DialogField("target", "대상", "choice", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.refs.index.mark",
                Label = "항목 표시",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_reference",
                Args = """{"action":"insert","kind":"index_entry","at":"{targets[0]}","index_entry":{"entry":"{entry}","subentry":"{subentry}","bold":"{bold}","italic":"{italic}"}}""",
                Dialog = new DialogSpec("색인 항목", new[] { new DialogField("entry", "기본 항목", "text", "\"{고른 글자}\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("subentry", "하위 항목", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("bold", "쪽 번호 굵게", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("italic", "쪽 번호 기울임", "bool", "false", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.mail.create.envelope",
                Label = "봉투",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_page",
                Args = """{"kind":"envelope","at":"start","envelope":{"size":"{size}","recipient":"{recipient}","sender":"{sender}"}}""",
                Dialog = new DialogSpec("봉투", new[] { new DialogField("recipient", "받는 사람 주소", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("sender", "보내는 사람 주소", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("size", "봉투 크기", "choice", "\"korean_small\"", new string[] { "korean_small", "DL", "C5", "C6", "size10" }, new string[] { "소봉투 (220×105mm)", "DL (220×110mm)", "C5 (229×162mm)", "C6 (162×114mm)", "10호 (241×105mm)" }) }),
            },
            new CommandSpec
            {
                Id = "word.mail.create.labels",
                Label = "레이블",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_page",
                Args = """{"kind":"labels","at":"end","labels":{"layout":"{layout}","text":"{text}","fill":"{all|first}"}}""",
                Dialog = new DialogSpec("레이블", new[] { new DialogField("text", "주소", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("layout", "규격", "choice", "\"A4_3x8\"", new string[] { "A4_2x7", "A4_2x8", "A4_3x7", "A4_3x8", "A4_3x10" }, new string[] { "2열×7행 (99.1×38.1mm)", "2열×8행 (99.1×33.9mm)", "3열×7행 (63.5×38.1mm)", "3열×8행 (70×36mm)", "3열×10행 (70×29.7mm)" }), new DialogField("fill", "채우기", "choice", "\"all\"", new string[] { "all", "first" }, new string[] { "한 장 모두 같은 주소", "첫 칸만" }) }),
            },
            new CommandSpec
            {
                Id = "word.mail.merge.start",
                Label = "편지 병합 시작",
                Class = RibbonCommandClass.View,
                Action = "startMerge",
                Args = """{"mode":"{value}"}""",
                MenuStyle = "mergeStart",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "편지", Args = """{"mode":"letter"}""", Detail = "본문은 그대로, 받는 사람마다 한 장씩" },
                    new CommandMenuSpec { Label = "레이블", Args = """{"command":"word.mail.merge.start.labels"}""", Detail = "먼저 [레이블]로 라벨 표를 만든 뒤 병합 필드를 넣어요" },
                    new CommandMenuSpec { Label = "전자 메일 메시지", Args = """{"command":"word.mail.merge.start.email"}""", Detail = "메일 보내기는 지원하지 않아요" },
                    new CommandMenuSpec { Label = "일반 Word 문서", Args = """{"mode":"none"}""", Detail = "병합 상태를 풀어요" },
                },
            },
            new CommandSpec
            {
                Id = "word.mail.merge.recipients",
                Label = "받는 사람 선택",
                Class = RibbonCommandClass.View,
                Action = "chooseRecipients",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "기존 목록 사용…", Args = """{"command":"word.mail.merge.recipients.existing"}""", Detail = "작업 폴더의 엑셀 파일과 시트, 첫 줄이 필드 이름이에요" },
                    new CommandMenuSpec { Label = "새 목록 입력…", Args = """{"command":"word.mail.merge.recipients.new"}""", Detail = "엑셀 파일을 새로 만들어 받는 사람을 채워 달라고 도우미에게 부탁해요" },
                    new CommandMenuSpec { Label = "Outlook 연락처에서 선택…", Args = """{"command":"word.mail.merge.recipients.outlook"}""", Detail = "Outlook 연락처는 쓸 수 없어요" },
                },
            },
            new CommandSpec
            {
                Id = "word.mail.merge.recipients.existing",
                Label = "기존 목록 사용",
                Class = RibbonCommandClass.File,
                Action = "chooseRecipients",
            },
            new CommandSpec
            {
                Id = "word.mail.merge.editlist",
                Label = "받는 사람 목록 편집",
                Class = RibbonCommandClass.View,
                Action = "editRecipients",
                Dialog = new DialogSpec("받는 사람 목록 편집", new[] { new DialogField("rows", "받는 사람", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.mail.fields.insert",
                Label = "병합 필드 삽입",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_field",
                Args = """{"action":"insert","kind":"merge_field","at":{"id":"{caret}","offset":"{caret offset}"},"name":"{value}"}""",
                MenuStyle = "mergeFields",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "{받는 사람 목록의 필드 이름마다 한 줄}", Args = """{"name":"{field}"}""" },
                    new CommandMenuSpec { Label = "필드 이름 직접 입력…", Args = """{"dialog":"mergeFieldName"}""" },
                },
                Dialog = new DialogSpec("병합 필드", new[] { new DialogField("name", "필드 이름", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.mail.preview.preview",
                Label = "결과 미리 보기",
                Class = RibbonCommandClass.View,
                Action = "togglePreview",
                Args = """{"on":"{toggle}"}""",
                Pressed = "mergePreview",
            },
            new CommandSpec
            {
                Id = "word.mail.finish.merge",
                Label = "완료 및 병합",
                Class = RibbonCommandClass.File,
                Action = "finishMerge",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "개별 문서 편집…", Args = """{"mode":"documents"}""", Detail = "받는 사람마다 한 장씩 따로 저장한 .docx 파일을 작업 폴더에 만들어요" },
                    new CommandMenuSpec { Label = "전자 메일 메시지 보내기…", Args = """{"command":"word.mail.finish.merge.email"}""", Detail = "메일 보내기는 지원하지 않아요" },
                },
            },
            new CommandSpec
            {
                Id = "word.review.proofing.spell",
                Label = "맞춤법 및 문법 검사",
                Class = RibbonCommandClass.Assistant,
                Action = "askAssistant",
                Prompt = "이 문서의 맞춤법, 띄어쓰기, 문법을 검사해서 틀린 곳을 고쳐 줘. 어디를 어떻게 고쳤는지 알려 줘.",
            },
            new CommandSpec
            {
                Id = "word.review.proofing.thesaurus",
                Label = "동의어 사전",
                Class = RibbonCommandClass.Assistant,
                Action = "askAssistant",
                Prompt = "‘{선택한 글}’ 대신 쓸 수 있는 비슷한 말을 5개쯤 알려 줘. 문장에 어울리는 순서로 써 줘.",
            },
            new CommandSpec
            {
                Id = "word.review.proofing.wordcount",
                Label = "단어 개수",
                Class = RibbonCommandClass.View,
                Action = "showWordCount",
            },
            new CommandSpec
            {
                Id = "word.review.speech.readaloud",
                Label = "소리내어 읽기",
                Class = RibbonCommandClass.View,
                Action = "readAloud",
                Args = """{"from":"{caret or selection start}","voice":"ko-KR","rate":1.0}""",
                Pressed = "readAloud",
            },
            new CommandSpec
            {
                Id = "word.review.comments.new",
                Label = "새 메모",
                Class = RibbonCommandClass.Tool,
                Tool = "manage_comments",
                Args = """{"action":"add","target":"{targets[0]}","text":"{text}","author":"{author}"}""",
                Dialog = new DialogSpec("새 메모", new[] { new DialogField("text", "메모 내용", "text", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.review.comments.show",
                Label = "메모 표시",
                Class = RibbonCommandClass.View,
                Action = "toggleComments",
                Args = """{"on":"{toggle}"}""",
                Pressed = "comments",
            },
            new CommandSpec
            {
                Id = "word.review.track.track",
                Label = "변경 내용 추적",
                Class = RibbonCommandClass.Tool,
                Tool = "manage_revisions",
                Args = """{"action":"{track_on|track_off}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "변경 내용 추적 켜기", Args = """{"action":"track_on"}""", Detail = "이 뒤로 고치는 글은 삽입·삭제로 남아요" },
                    new CommandMenuSpec { Label = "변경 내용 추적 끄기", Args = """{"action":"track_off"}""" },
                    new CommandMenuSpec { Label = "사용자 이름 바꾸기…", Args = """{"dialog":"trackAuthor"}""", Detail = "남는 이름. 기본은 Windows 사용자 이름" },
                },
            },
            new CommandSpec
            {
                Id = "word.review.track.display",
                Label = "검토할 내용 표시",
                Class = RibbonCommandClass.View,
                Action = "setMarkup",
                Args = """{"markup":"{value}"}""",
                Value = "markup",
                MenuStyle = "markupDisplay",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "모든 변경 내용", Args = """{"markup":"all"}""", Detail = "삽입은 밑줄, 삭제는 취소선, 왼쪽 막대와 풍선" },
                    new CommandMenuSpec { Label = "간단한 변경 내용", Args = """{"markup":"simple"}""", Detail = "글은 고친 모습으로, 왼쪽 막대만" },
                    new CommandMenuSpec { Label = "변경 내용 없음", Args = """{"markup":"none"}""", Detail = "고친 모습으로만" },
                    new CommandMenuSpec { Label = "원본", Args = """{"markup":"original"}""", Detail = "고치기 전 모습" },
                },
            },
            new CommandSpec
            {
                Id = "word.review.track.showmarkup.format",
                Label = "서식 변경",
                Class = RibbonCommandClass.Disabled,
                Reason = "서식 변경은 아직 따로 나누어 표시하지 않아요. 고친 모습은 삽입 및 삭제 설정을 따라요.",
            },
            new CommandSpec
            {
                Id = "word.review.track.showmarkup.author",
                Label = "특정 사용자",
                Class = RibbonCommandClass.Disabled,
                Reason = "사용자별로 나누어 표시하는 기능은 아직 없어요.",
            },
            new CommandSpec
            {
                Id = "word.review.track.showmarkup",
                Label = "변경 내용 표시",
                Class = RibbonCommandClass.View,
                Action = "setMarkupKinds",
                Args = """{"kind":"{comments|insdel|format|author:<name>}","on":"{toggle}"}""",
                MenuStyle = "showMarkup",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "메모", Args = """{"kind":"comments"}""" },
                    new CommandMenuSpec { Label = "삽입 및 삭제", Args = """{"kind":"insdel"}""" },
                    new CommandMenuSpec { Label = "서식", Args = """{"command":"word.review.track.showmarkup.format"}""" },
                    new CommandMenuSpec { Label = "특정 사용자", Args = """{"command":"word.review.track.showmarkup.author"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.review.changes.accept",
                Label = "적용",
                Class = RibbonCommandClass.Tool,
                Tool = "manage_revisions",
                Args = """{"action":"accept","scope":"{range|all}","targets":"{targets}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "적용", Args = """{"scope":"range"}""", Detail = "고른 부분(없으면 커서 자리 한 개)의 변경을 받아들여요" },
                    new CommandMenuSpec { Label = "적용하고 다음으로 이동", Args = """{"scope":"range","then":"next"}""" },
                    new CommandMenuSpec { Label = "모든 변경 내용 적용", Args = """{"scope":"all"}""" },
                    new CommandMenuSpec { Label = "모든 변경 내용 적용하고 추적 중지", Args = """{"scope":"all","then":"track_off"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.review.changes.reject",
                Label = "적용 안 함",
                Class = RibbonCommandClass.Tool,
                Tool = "manage_revisions",
                Args = """{"action":"reject","scope":"{range|all}","targets":"{targets}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "적용 안 함", Args = """{"scope":"range"}""", Detail = "고른 부분의 변경을 되돌려요" },
                    new CommandMenuSpec { Label = "적용 안 함 후 다음으로 이동", Args = """{"scope":"range","then":"next"}""" },
                    new CommandMenuSpec { Label = "모든 변경 내용 적용 안 함", Args = """{"scope":"all"}""" },
                    new CommandMenuSpec { Label = "모든 변경 내용 적용 안 하고 추적 중지", Args = """{"scope":"all","then":"track_off"}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.review.changes.prev",
                Label = "이전",
                Class = RibbonCommandClass.View,
                Action = "goToRevision",
                Args = """{"direction":"prev"}""",
            },
            new CommandSpec
            {
                Id = "word.review.changes.next",
                Label = "다음",
                Class = RibbonCommandClass.View,
                Action = "goToRevision",
                Args = """{"direction":"next"}""",
            },
            new CommandSpec
            {
                Id = "word.review.compare.compare",
                Label = "비교",
                Class = RibbonCommandClass.File,
                Action = "compareDocuments",
                Args = """{"original":"{docx file}","revised":"{docx file}","author":"{author}"}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "비교…", Args = """{"dialog":"compare"}""", Detail = "원본과 고친 문서를 골라 달라진 곳을 추적 변경으로 보여 주는 새 문서를 만들어요" },
                    new CommandMenuSpec { Label = "결합…", Args = """{"command":"word.review.compare.combine"}""", Detail = "여러 사람이 고친 문서 합치기는 Word에서 해요" },
                },
                Dialog = new DialogSpec("문서 비교", new[] { new DialogField("original", "원본 문서", "choice", "\"\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("revised", "고친 문서", "choice", "\"{지금 문서}\"", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("author", "고친 사람 이름", "text", "\"{Windows 사용자 이름}\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.review.protect.restrict",
                Label = "편집 제한",
                Class = RibbonCommandClass.Tool,
                Tool = "set_document_settings",
                Args = """{"protection":{"mode":"{none|read_only|comments_only|tracked_changes_only}","enforce":true}}""",
                Dialog = new DialogSpec("편집 제한", new[] { new DialogField("mode", "허용할 편집", "choice", "\"none\"", new string[] { "none", "read_only", "comments_only", "tracked_changes_only" }, new string[] { "제한 없음", "읽기만", "메모 달기만", "변경 내용 추적만" }) }),
            },
            new CommandSpec
            {
                Id = "word.view.views.read",
                Label = "읽기 모드",
                Class = RibbonCommandClass.View,
                Action = "setView",
                Args = """{"view":"read","multi":false,"rib":false}""",
                Pressed = "readView",
            },
            new CommandSpec
            {
                Id = "word.view.views.print",
                Label = "인쇄 모양",
                Class = RibbonCommandClass.View,
                Action = "setView",
                Args = """{"view":"print","rib":true}""",
                Pressed = "printView",
            },
            new CommandSpec
            {
                Id = "word.view.views.web",
                Label = "웹 모양",
                Class = RibbonCommandClass.View,
                Action = "setView",
                Args = """{"view":"web","multi":false}""",
                Pressed = "webView",
            },
            new CommandSpec
            {
                Id = "word.view.views.outline",
                Label = "개요",
                Class = RibbonCommandClass.View,
                Action = "setView",
                Args = """{"view":"outline"}""",
                Pressed = "outlineView",
            },
            new CommandSpec
            {
                Id = "word.view.views.draft",
                Label = "초안",
                Class = RibbonCommandClass.View,
                Action = "setView",
                Args = """{"view":"draft"}""",
                Pressed = "draftView",
            },
            new CommandSpec
            {
                Id = "word.view.move.vertical",
                Label = "세로",
                Class = RibbonCommandClass.View,
                Action = "setPageMovement",
                Args = """{"multi":false}""",
                Pressed = "verticalPages",
            },
            new CommandSpec
            {
                Id = "word.view.move.side",
                Label = "나란히",
                Class = RibbonCommandClass.View,
                Action = "setPageMovement",
                Args = """{"multi":true,"zoom":"min(zoom, 70)"}""",
                Pressed = "sidePages",
            },
            new CommandSpec
            {
                Id = "word.view.show.ruler",
                Label = "눈금자",
                Class = RibbonCommandClass.View,
                Action = "toggleView",
                Args = """{"ruler":"{toggle}"}""",
                Need = "notWebView",
                Pressed = "ruler",
            },
            new CommandSpec
            {
                Id = "word.view.show.grid",
                Label = "눈금선",
                Class = RibbonCommandClass.View,
                Action = "toggleView",
                Args = """{"grid":"{toggle}"}""",
                Need = "printView",
                Pressed = "gridlines",
            },
            new CommandSpec
            {
                Id = "word.view.show.nav",
                Label = "탐색 창",
                Class = RibbonCommandClass.View,
                Action = "toggleView",
                Args = """{"nav":"{toggle}"}""",
                Pressed = "navPane",
            },
            new CommandSpec
            {
                Id = "word.view.zoom.dialog",
                Label = "확대/축소",
                Class = RibbonCommandClass.View,
                Action = "openZoomDialog",
                Need = "zoomDialog",
                Dialog = new DialogSpec("확대/축소", new[] { new DialogField("zoom", "배율", "choice", "\"100\"", new string[] { "200", "150", "120", "100", "90", "80", "70", "60", "50" }, new string[] { "200%", "150%", "120%", "100%", "90%", "80%", "70%", "60%", "50%" }), new DialogField("custom", "직접 입력(50~200)", "number", "", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.view.zoom.100",
                Label = "100%",
                Class = RibbonCommandClass.View,
                Action = "setZoom",
                Args = """{"zoom":100,"multi":false}""",
            },
            new CommandSpec
            {
                Id = "word.view.zoom.onepage",
                Label = "한 페이지",
                Class = RibbonCommandClass.View,
                Action = "fitPage",
                Args = """{"zoom":"fit one page","multi":false,"view":"print"}""",
            },
            new CommandSpec
            {
                Id = "word.view.zoom.multi",
                Label = "여러 페이지",
                Class = RibbonCommandClass.View,
                Action = "fitMulti",
                Args = """{"zoom":50,"multi":true,"view":"print"}""",
            },
            new CommandSpec
            {
                Id = "word.view.zoom.pagewidth",
                Label = "페이지 너비",
                Class = RibbonCommandClass.View,
                Action = "fitWidth",
                Args = """{"zoom":"fit width","multi":false,"view":"print"}""",
            },
            new CommandSpec
            {
                Id = "word.trailing.undo",
                Label = "실행 취소",
                Class = RibbonCommandClass.File,
                Action = "undo",
                WhileLocked = true,
            },
            new CommandSpec
            {
                Id = "word.trailing.redo",
                Label = "다시 실행",
                Class = RibbonCommandClass.File,
                Action = "redo",
                WhileLocked = true,
            },
            new CommandSpec
            {
                Id = "word.trailing.collapse",
                Label = "리본 접기",
                Class = RibbonCommandClass.View,
                Action = "toggleRibbon",
                Args = """{"rib":"{toggle}"}""",
            },
            new CommandSpec
            {
                Id = "word.file.save",
                Label = "저장",
                Class = RibbonCommandClass.File,
                Action = "save",
            },
            new CommandSpec
            {
                Id = "word.file.saveas",
                Label = "다른 이름으로 저장",
                Class = RibbonCommandClass.File,
                Action = "saveAs",
                WhileLocked = true,
            },
            new CommandSpec
            {
                Id = "word.file.pdf",
                Label = "PDF로 저장",
                Class = RibbonCommandClass.File,
                Action = "exportPdf",
                WhileLocked = true,
            },
            new CommandSpec
            {
                Id = "word.file.print",
                Label = "인쇄",
                Class = RibbonCommandClass.File,
                Action = "print",
                WhileLocked = true,
            },
            new CommandSpec
            {
                Id = "word.file.info",
                Label = "문서 정보",
                Class = RibbonCommandClass.File,
                Action = "showInfo",
                WhileLocked = true,
            },
            new CommandSpec
            {
                Id = "word.file.openword",
                Label = "Word에서 열기",
                Class = RibbonCommandClass.External,
                Action = "openInOriginalApp",
            },
            new CommandSpec
            {
                Id = "word.status.page",
                Label = "쪽으로 이동",
                Class = RibbonCommandClass.View,
                Action = "goToPage",
                Args = """{"page":"{n}"}""",
                Dialog = new DialogSpec("이동", new[] { new DialogField("page", "몇 쪽으로 갈까요?", "number", "1", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.status.prevchange",
                Label = "이전 바뀐 곳",
                Class = RibbonCommandClass.View,
                Action = "moveChange",
                Args = """{"step":-1}""",
                Need = "changes",
            },
            new CommandSpec
            {
                Id = "word.status.nextchange",
                Label = "다음 바뀐 곳",
                Class = RibbonCommandClass.View,
                Action = "moveChange",
                Args = """{"step":1}""",
                Need = "changes",
            },
            new CommandSpec
            {
                Id = "word.status.revertall",
                Label = "전부 되돌리기",
                Class = RibbonCommandClass.File,
                Action = "revertAll",
            },
            new CommandSpec
            {
                Id = "word.status.zoomout",
                Label = "축소",
                Class = RibbonCommandClass.View,
                Action = "zoomBy",
                Args = """{"delta":-10}""",
                Need = "zoomOut",
            },
            new CommandSpec
            {
                Id = "word.status.zoomin",
                Label = "확대",
                Class = RibbonCommandClass.View,
                Action = "zoomBy",
                Args = """{"delta":10}""",
                Need = "zoomIn",
            },
            new CommandSpec
            {
                Id = "word.status.zoomslider",
                Label = "확대/축소 슬라이더",
                Class = RibbonCommandClass.View,
                Action = "setZoom",
                Args = """{"zoom":"{value}"}""",
            },
            new CommandSpec
            {
                Id = "word.nav.close",
                Label = "탐색 창 닫기",
                Class = RibbonCommandClass.View,
                Action = "toggleView",
                Args = """{"nav":false}""",
            },
            new CommandSpec
            {
                Id = "word.nav.pane.head",
                Label = "제목",
                Class = RibbonCommandClass.View,
                Action = "setNavPane",
                Args = """{"pane":"head"}""",
            },
            new CommandSpec
            {
                Id = "word.nav.pane.pages",
                Label = "페이지",
                Class = RibbonCommandClass.View,
                Action = "setNavPane",
                Args = """{"pane":"pages"}""",
            },
            new CommandSpec
            {
                Id = "word.nav.pane.res",
                Label = "결과",
                Class = RibbonCommandClass.View,
                Action = "setNavPane",
                Args = """{"pane":"res"}""",
            },
            new CommandSpec
            {
                Id = "word.nav.search",
                Label = "문서 검색",
                Class = RibbonCommandClass.View,
                Action = "search",
                Args = """{"query":"{text}","match_case":false,"whole_word":false}""",
            },
            new CommandSpec
            {
                Id = "word.nav.goto.heading",
                Label = "제목으로 이동",
                Class = RibbonCommandClass.View,
                Action = "goToBlock",
                Args = """{"id":"{heading block id}"}""",
            },
            new CommandSpec
            {
                Id = "word.nav.goto.page",
                Label = "쪽으로 이동(썸네일)",
                Class = RibbonCommandClass.View,
                Action = "goToPage",
                Args = """{"page":"{n}"}""",
            },
            new CommandSpec
            {
                Id = "word.layout.setup.linenumbers.suppress",
                Label = "현재 단락에서 줄 번호 사용 안 함",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","suppress_line_numbers":true}""",
            },
            new CommandSpec
            {
                Id = "word.mail.merge.start.labels",
                Label = "레이블 병합",
                Class = RibbonCommandClass.Disabled,
                Reason = "레이블 병합은 아직 만들지 않았어요. 라벨 표를 만든 뒤 병합 필드를 넣어 주세요.",
            },
            new CommandSpec
            {
                Id = "word.mail.merge.start.email",
                Label = "전자 메일 메시지",
                Class = RibbonCommandClass.Disabled,
                Reason = "메일은 이 앱에서 보내지 않아요. ‘편지’로 병합한 파일을 만든 뒤 메일 프로그램으로 보내 주세요.",
            },
            new CommandSpec
            {
                Id = "word.mail.merge.recipients.new",
                Label = "새 목록 입력",
                Class = RibbonCommandClass.Assistant,
                Action = "askAssistant",
                Prompt = "받는 사람 목록을 새 엑셀 파일로 만들어 줘. 이름, 주소, 전화번호 칸을 두고 예시로 두 줄만 채워 줘.",
            },
            new CommandSpec
            {
                Id = "word.mail.merge.recipients.outlook",
                Label = "Outlook 연락처에서 선택",
                Class = RibbonCommandClass.Disabled,
                Reason = "Outlook 연락처는 읽을 수 없어요. 연락처를 엑셀로 내보낸 뒤 ‘기존 목록 사용’으로 골라 주세요.",
            },
            new CommandSpec
            {
                Id = "word.mail.finish.merge.email",
                Label = "전자 메일 메시지 보내기",
                Class = RibbonCommandClass.Disabled,
                Reason = "메일 보내기는 지원하지 않아요. ‘개별 문서 편집’으로 파일을 만든 뒤 보내 주세요.",
            },
            new CommandSpec
            {
                Id = "word.review.compare.combine",
                Label = "결합",
                Class = RibbonCommandClass.External,
                Action = "openInOriginalApp",
            },
            new CommandSpec
            {
                Id = "word.table.cell.bold",
                Label = "칸 글자 굵게",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{cell targets}","bold":true}""",
                CellsOnly = true,
            },
            new CommandSpec
            {
                Id = "word.table.rows.above",
                Label = "위에 행 삽입",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"insert_rows","row":"{row}","position":"before","count":1}""",
            },
            new CommandSpec
            {
                Id = "word.table.rows.below",
                Label = "아래에 행 삽입",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"insert_rows","row":"{row}","position":"after","count":1}""",
            },
            new CommandSpec
            {
                Id = "word.table.cols.left",
                Label = "왼쪽에 열 삽입",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"insert_columns","column":"{column}","position":"before","count":1}""",
            },
            new CommandSpec
            {
                Id = "word.table.cols.right",
                Label = "오른쪽에 열 삽입",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"insert_columns","column":"{column}","position":"after","count":1}""",
            },
            new CommandSpec
            {
                Id = "word.table.rows.delete",
                Label = "행 삭제",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"delete_rows","row":"{row}","count":"{selected rows}"}""",
            },
            new CommandSpec
            {
                Id = "word.table.cols.delete",
                Label = "열 삭제",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"delete_columns","column":"{column}","count":"{selected columns}"}""",
            },
            new CommandSpec
            {
                Id = "word.table.delete",
                Label = "표 삭제",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"delete_table"}""",
            },
            new CommandSpec
            {
                Id = "word.table.merge",
                Label = "셀 병합",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"merge_cells","range":"{range}"}""",
            },
            new CommandSpec
            {
                Id = "word.table.split",
                Label = "셀 분할",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"split_cell","row":"{row}","column":"{column}","split":{"rows":"{rows}","columns":"{columns}"}}""",
                Dialog = new DialogSpec("셀 분할", new[] { new DialogField("columns", "열 수", "number", "2", System.Array.Empty<string>(), System.Array.Empty<string>()), new DialogField("rows", "행 수", "number", "1", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.table.align.left",
                Label = "칸 왼쪽 맞춤",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"format","range":"{range}","format":{"h_align":"left"}}""",
            },
            new CommandSpec
            {
                Id = "word.table.align.center",
                Label = "칸 가운데 맞춤",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"format","range":"{range}","format":{"h_align":"center"}}""",
            },
            new CommandSpec
            {
                Id = "word.table.align.right",
                Label = "칸 오른쪽 맞춤",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"format","range":"{range}","format":{"h_align":"right"}}""",
            },
            new CommandSpec
            {
                Id = "word.table.valign.top",
                Label = "위쪽",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"format","range":"{range}","format":{"v_align":"top"}}""",
            },
            new CommandSpec
            {
                Id = "word.table.valign.center",
                Label = "가운데",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"format","range":"{range}","format":{"v_align":"center"}}""",
            },
            new CommandSpec
            {
                Id = "word.table.valign.bottom",
                Label = "아래쪽",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"format","range":"{range}","format":{"v_align":"bottom"}}""",
            },
            new CommandSpec
            {
                Id = "word.table.size.equalcols",
                Label = "열 너비 같게",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"distribute","axis":"columns"}""",
            },
            new CommandSpec
            {
                Id = "word.table.size.equalrows",
                Label = "행 높이 같게",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"distribute","axis":"rows"}""",
            },
            new CommandSpec
            {
                Id = "word.table.size.autofit",
                Label = "자동 맞춤",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"set_style","style":{"autofit":"{value}"}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "내용에 맞춤", Args = """{"style":{"autofit":"contents"}}""" },
                    new CommandMenuSpec { Label = "창에 맞춤", Args = """{"style":{"autofit":"window"}}""" },
                    new CommandMenuSpec { Label = "고정 너비", Args = """{"style":{"autofit":"fixed"}}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.table.style.header",
                Label = "머리글 행",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"set_style","style":{"header_row":"{toggle}"}}""",
            },
            new CommandSpec
            {
                Id = "word.table.style.banded",
                Label = "줄무늬 행",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"set_style","style":{"banded_rows":"{toggle}"}}""",
            },
            new CommandSpec
            {
                Id = "word.table.style.borders",
                Label = "테두리",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"format","range":"{range or whole table}","format":{"borders":{"sides":"{sides}","style":"single","width_pt":0.5,"color":"#999999"}}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "모든 테두리", Args = """{"format":{"borders":{"sides":["all"],"style":"single","width_pt":0.5,"color":"#999999"}}}""" },
                    new CommandMenuSpec { Label = "바깥쪽 테두리", Args = """{"format":{"borders":{"sides":["outer"],"style":"single","width_pt":1,"color":"#000000"}}}""" },
                    new CommandMenuSpec { Label = "안쪽 테두리", Args = """{"format":{"borders":{"sides":["inside_h","inside_v"],"style":"single","width_pt":0.5,"color":"#999999"}}}""" },
                    new CommandMenuSpec { Label = "위쪽 테두리", Args = """{"format":{"borders":{"sides":["top"],"style":"single","width_pt":0.5,"color":"#999999"}}}""" },
                    new CommandMenuSpec { Label = "아래쪽 테두리", Args = """{"format":{"borders":{"sides":["bottom"],"style":"single","width_pt":0.5,"color":"#999999"}}}""" },
                    new CommandMenuSpec { Label = "테두리 없음", Args = """{"format":{"borders":{"sides":["all"],"style":"none"}}}""" },
                },
            },
            new CommandSpec
            {
                Id = "word.table.style.shading",
                Label = "음영",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"format","range":"{range or whole table}","format":{"shading":"{color}"}}""",
                Dialog = new DialogSpec("음영", new[] { new DialogField("color", "색", "color", "\"F2F2F2\"", System.Array.Empty<string>(), System.Array.Empty<string>()) }),
            },
            new CommandSpec
            {
                Id = "word.table.style.preset",
                Label = "표 스타일",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_table",
                Args = """{"table":"{table}","action":"set_style","style":{"preset":"{value}"}}""",
                Menu = new[]
                {
                    new CommandMenuSpec { Label = "격자(기본)", Args = """{"style":{"preset":"grid"}}""", Detail = "가는 회색 테두리, 첫 줄 음영" },
                    new CommandMenuSpec { Label = "보통", Args = """{"style":{"preset":"plain"}}""", Detail = "테두리 없음" },
                    new CommandMenuSpec { Label = "줄무늬", Args = """{"style":{"preset":"banded"}}""", Detail = "한 줄씩 옅은 음영" },
                    new CommandMenuSpec { Label = "가로선만", Args = """{"style":{"preset":"horizontal"}}""", Detail = "가로 테두리만" },
                },
            },
        };
    }
}
