using System;
using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    internal static class HwpCommandTable
    {
        public static IReadOnlyList<HwpCommandSpec> All { get; } = new[]
        {
            new HwpCommandSpec
            {
                Id = "hwp.edit.clipboard.paste",
                Label = "붙이기",
                Class = RibbonCommandClass.Clipboard,
                Action = "paste",
                Tool = "edit_text",
                Args = """{"ops":[{"op":"paste","id":"{id}","offset":"{offset}","paragraphs":"{clip paragraphs}"}]}""",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "원래 모양대로 붙이기", Args = """{"mode":"keep_source"}""", Detail = "내부 복사본이면 글자·문단 모양 그대로. 바깥에서 온 글은 굵게·기울임·밑줄·크기·색·목록·표를 가져와요" },
                    new HwpMenuSpec { Label = "현재 모양에 맞춰 붙이기", Args = """{"mode":"merge"}""", Detail = "글만 가져오고 모양은 붙이는 자리를 따라요" },
                    new HwpMenuSpec { Label = "글자만 붙이기", Args = """{"mode":"text_only"}""", Detail = "모양 없이 글만 붙여요 (Ctrl+Shift+V)" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.clipboard.cut",
                Label = "오려 두기",
                Class = RibbonCommandClass.Clipboard,
                Action = "cut",
                Tool = "edit_text",
                Args = """{"ops":[{"op":"delete","id":"{id}","start":"{start}","end":"{end}"}]}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.clipboard.copy",
                Label = "복사하기",
                Class = RibbonCommandClass.Clipboard,
                Action = "copy",
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.clipboard.formatpainter",
                Label = "모양 복사",
                Class = RibbonCommandClass.Clipboard,
                Action = "formatPainter",
                Tool = "format_text",
                Args = """{"targets":"{targets}","font":"{src.font}","size":"{src.size}","bold":"{src.bold}","italic":"{src.italic}","underline":"{src.underline}","color":"{src.color}"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.clipboard.selectall",
                Label = "모두 선택",
                Class = RibbonCommandClass.View,
                Action = "selectAll",
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.format.char",
                Label = "글자 모양",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","font":"{font}","size":"{size}","bold":"{bold}","italic":"{italic}","underline":"{underline}","strike":"{strike}","color":"{color}","highlight":"{shade}","vertical_align":"{vertical_align}","scale_percent":"{scale}","spacing_pt":"{spacing*cur.size/100}"}""",
                Dialog = new WordDialogSpec("글자 모양", new[]
                {
                    new WordDialogField("font", "글꼴", "text", "\"{cur.font}\"", new string[] { "함초롬바탕", "함초롬돋움", "맑은 고딕", "바탕", "돋움", "굴림", "궁서", "나눔고딕", "나눔명조", "HY견고딕", "HY헤드라인M", "Arial", "Times New Roman", "Calibri", "Consolas" }, Array.Empty<string>()),
                    new WordDialogField("size", "기준 크기 (pt)", "number", "\"{cur.size}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("scale", "장평 (%)", "number", "100", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("spacing", "자간 (%)", "number", "0", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("color", "글자 색", "color", "\"auto\"", new string[] { "auto" }, Array.Empty<string>()),
                    new WordDialogField("shade", "음영 색", "color", "\"none\"", new string[] { "none" }, Array.Empty<string>()),
                    new WordDialogField("bold", "진하게", "bool", "\"{cur.bold}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("italic", "기울임", "bool", "\"{cur.italic}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("underline", "밑줄", "choice", "\"none\"", new string[] { "none", "single" }, Array.Empty<string>()),
                    new WordDialogField("strike", "취소선", "choice", "\"none\"", new string[] { "none", "single" }, Array.Empty<string>()),
                    new WordDialogField("vertical_align", "위/아래 첨자", "choice", "\"baseline\"", new string[] { "baseline", "superscript", "subscript" }, Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.format.para",
                Label = "문단 모양",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","align":"{align}","indent_left_cm":"{left_pt/28.3465}","indent_right_cm":"{right_pt/28.3465}","first_line_cm":"{first_pt/28.3465}","hanging_cm":"{first_pt/28.3465}","line_spacing":{"mode":"multiple","value":"{line_value/160}"},"space_before_pt":"{space_before_pt}","space_after_pt":"{space_after_pt}","keep_with_next":"{keep_with_next}","page_break_before":"{page_break_before}"}""",
                Dialog = new WordDialogSpec("문단 모양", new[]
                {
                    new WordDialogField("align", "정렬", "choice", "\"{cur.align}\"", new string[] { "justify", "left", "center", "right", "distribute", "divide" }, new string[] { "양쪽", "왼쪽", "가운데", "오른쪽", "배분", "나눔" }),
                    new WordDialogField("left_pt", "왼쪽 여백 (pt)", "number", "\"{cur.indentLeftPt}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("right_pt", "오른쪽 여백 (pt)", "number", "\"{cur.indentRightPt}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("first_mode", "첫 줄", "choice", "\"none\"", new string[] { "none", "indent", "outdent" }, new string[] { "보통", "들여쓰기", "내어쓰기" }),
                    new WordDialogField("first_pt", "첫 줄 크기 (pt)", "number", "10", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("line_mode", "줄 간격 종류", "choice", "\"percent\"", new string[] { "percent", "fixed", "at_least" }, new string[] { "글자에 따라 (%)", "고정 값 (pt)", "최소 (pt)" }),
                    new WordDialogField("line_value", "줄 간격 값", "number", "\"{cur.lineSpacingPercent}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("space_before_pt", "문단 위 (pt)", "number", "\"{cur.spaceBeforePt}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("space_after_pt", "문단 아래 (pt)", "number", "\"{cur.spaceAfterPt}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("keep_with_next", "다음 문단과 함께", "bool", "\"{cur.keepWithNext}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("page_break_before", "문단 앞에서 쪽 나눔", "bool", "\"{cur.pageBreakBefore}\"", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.format.style",
                Label = "스타일",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","style":"{value}"}""",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "바탕글", Args = """{"style":"바탕글"}""", Detail = "모든 문단의 기본 스타일" },
                    new HwpMenuSpec { Label = "본문", Args = """{"style":"본문"}""", Detail = "본문 글" },
                    new HwpMenuSpec { Label = "개요 1", Args = """{"style":"개요 1"}""", Detail = "굵게, 16pt" },
                    new HwpMenuSpec { Label = "개요 2", Args = """{"style":"개요 2"}""", Detail = "굵게, 13pt" },
                    new HwpMenuSpec { Label = "개요 3", Args = """{"style":"개요 3"}""" },
                    new HwpMenuSpec { Label = "개요 4", Args = """{"style":"개요 4"}""" },
                    new HwpMenuSpec { Label = "개요 5", Args = """{"style":"개요 5"}""" },
                    new HwpMenuSpec { Label = "개요 6", Args = """{"style":"개요 6"}""" },
                    new HwpMenuSpec { Label = "개요 7", Args = """{"style":"개요 7"}""" },
                    new HwpMenuSpec { Label = "캡션", Args = """{"style":"캡션"}""", Detail = "표·그림 설명" },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Kind = HwpMenuKind.Heading, Label = "문서에 있는 스타일", Detail = "문서에서 쓰는 다른 스타일이 있으면 여기에 이어서 나와요" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.page.margins",
                Label = "쪽 여백",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"scope":"document","margins":"{value}"}""",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "한글 기본", Args = """{"margins":{"top_cm":3.5,"bottom_cm":3.0,"left_cm":3.0,"right_cm":3.0,"header_cm":2.0,"footer_cm":1.5}}""", Detail = "위쪽 20 · 아래쪽 15 · 왼쪽 30 · 오른쪽 30 · 머리말 15 · 꼬리말 15 mm" },
                    new HwpMenuSpec { Label = "좁게", Args = """{"margins":{"top_cm":2.0,"bottom_cm":2.0,"left_cm":1.5,"right_cm":1.5,"header_cm":1.0,"footer_cm":1.0}}""", Detail = "위쪽 10 · 아래쪽 10 · 왼쪽 15 · 오른쪽 15 · 머리말 10 · 꼬리말 10 mm" },
                    new HwpMenuSpec { Label = "보통", Args = """{"margins":"normal"}""", Detail = "Word의 보통(2.54cm)과 같아요" },
                    new HwpMenuSpec { Label = "넓게", Args = """{"margins":"wide"}""", Detail = "위·아래 2.54cm, 왼쪽·오른쪽 5.08cm" },
                    new HwpMenuSpec { Label = "공문서", Args = """{"margins":"official"}""", Detail = "위 3.0 · 아래 1.5 · 왼쪽 2.0 · 오른쪽 2.0 cm, 머리말 1.5 · 꼬리말 1.0 cm" },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "편집 용지…", Args = """{"command":"hwp.page.setup.paper"}""", Detail = "용지 크기, 방향, 여백을 한 번에 (F7)" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.page.portrait",
                Label = "세로",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"scope":"document","orientation":"portrait"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.page.landscape",
                Label = "가로",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"scope":"document","orientation":"landscape"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.page.columns",
                Label = "단",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"scope":"document","columns":"{value}"}""",
                Reason = "여러 단은 아직 한글 파일에 저장되지 않아요. 한글에서 단을 나눠 주세요.",
                Gates = new[] { "Columns" },
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "하나", Args = """{"columns":{"preset":"one"}}""", Gates = new[] { "Columns" }, Reason = "여러 단은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "둘", Args = """{"columns":{"preset":"two"}}""", Gates = new[] { "Columns" }, Reason = "여러 단은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "셋", Args = """{"columns":{"preset":"three"}}""", Gates = new[] { "Columns" }, Reason = "여러 단은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "왼쪽", Args = """{"columns":{"preset":"left"}}""", Detail = "왼쪽 단이 좁아요", Gates = new[] { "Columns" }, Reason = "여러 단은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "오른쪽", Args = """{"columns":{"preset":"right"}}""", Detail = "오른쪽 단이 좁아요", Gates = new[] { "Columns" }, Reason = "여러 단은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "단 설정…", Args = """{"dialog":"columns"}""", Detail = "단 개수, 간격, 사이 선", Gates = new[] { "Columns" }, Reason = "여러 단은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                },
                Dialog = new WordDialogSpec("단 설정", new[]
                {
                    new WordDialogField("count", "단 개수", "number", "2", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("space_mm", "단 사이 간격 (mm)", "number", "8", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("line_between", "단 사이 선", "bool", "false", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("equal", "같은 너비", "bool", "true", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.replace.convert",
                Label = "글자 바꾸기",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","case":"{value}"}""",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "영문 대문자로", Args = """{"case":"upper"}""", Detail = "apple → APPLE" },
                    new HwpMenuSpec { Label = "영문 소문자로", Args = """{"case":"lower"}""", Detail = "APPLE → apple" },
                    new HwpMenuSpec { Label = "문장 첫 글자만 대문자로", Args = """{"case":"sentence"}""", Detail = "hello world. next → Hello world. Next" },
                    new HwpMenuSpec { Label = "낱말 첫 글자만 대문자로", Args = """{"case":"title"}""", Detail = "hello world → Hello World" },
                    new HwpMenuSpec { Label = "대·소문자 서로 바꾸기", Args = """{"case":"toggle"}""", Detail = "Hello → hELLO" },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "반각으로 (ＡＢＣ１２３ → ABC123)", Args = """{"width":"half"}""", Detail = "전각 영문·숫자·기호를 반각으로", Gates = new[] { "WordTextWidth" }, Reason = "전각·반각 바꾸기는 아직 준비 중이에요." },
                    new HwpMenuSpec { Label = "전각으로 (ABC123 → ＡＢＣ１２３)", Args = """{"width":"full"}""", Detail = "반각 영문·숫자·기호를 전각으로", Gates = new[] { "WordTextWidth" }, Reason = "전각·반각 바꾸기는 아직 준비 중이에요." },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "한자로 바꾸기", Args = """{"command":"hwp.insert.helper.hanja"}""", Detail = "도우미가 문맥에 맞는 한자로 바꿔요 (F9)" },
                    new HwpMenuSpec { Label = "한글로 바꾸기", Args = """{"command":"hwp.edit.replace.tohangul"}""", Detail = "도우미가 한자를 한글 음으로 바꿔요" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.replace.tohangul",
                Label = "한글로 바꾸기",
                Class = RibbonCommandClass.Assistant,
                Action = "askAssistant",
                Prompt = "고른 한자(‘{선택한 글}’)를 한글 음으로 바꿔 줘. 문서의 나머지는 그대로 둬.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.insert.table",
                Label = "표",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_table",
                Args = """{"after":"{caret block id or end}","rows":"{rows × columns 빈 칸}","header":"{header}","style":"grid","align":"{align}"}""",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "2 × 2", Args = """{"rows":2,"columns":2}""" },
                    new HwpMenuSpec { Label = "3 × 3", Args = """{"rows":3,"columns":3}""" },
                    new HwpMenuSpec { Label = "4 × 4", Args = """{"rows":4,"columns":4}""" },
                    new HwpMenuSpec { Label = "5 × 3", Args = """{"rows":5,"columns":3}""" },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "표 만들기…", Args = """{"dialog":"tableMake"}""", Detail = "줄 수와 칸 수를 정해요" },
                },
                Dialog = new WordDialogSpec("표 만들기", new[]
                {
                    new WordDialogField("rows", "줄 수", "number", "3", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("columns", "칸 수", "number", "3", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("header", "첫 줄을 제목 줄로", "bool", "true", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("align", "표 맞춤", "choice", "\"left\"", new string[] { "left", "center", "right" }, new string[] { "왼쪽", "가운데", "오른쪽" }),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.insert.picture",
                Label = "그림",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_object",
                Args = """{"action":"insert","kind":"picture","at":"{caret}","picture":{"path":"{file}","alt":"{alt}"}}""",
                Reason = "그림을 넣을 문단을 골라 주세요.",
                Gates = new[] { "Pictures" },
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "파일에서 그림 넣기…", Args = """{"dialog":"pickPicture"}""", Detail = "png, jpg, gif, bmp. 작업 폴더 밖 파일은 앱이 읽어서 보내요", Gates = new[] { "Pictures" }, Reason = "그림은 아직 한글 파일에 저장되지 않아서 넣을 수 없어요." },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Kind = HwpMenuKind.Heading, Label = "그림은 한글에서 넣으면 저장돼도 그대로 남아요" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.insert.shapes",
                Label = "도형",
                Class = RibbonCommandClass.External,
                Action = "openOriginalApp",
                Reason = "도형은 한글 파일에 저장되지 않아서 이 앱에서는 넣을 수 없어요. 한글에서 넣어 주세요.",
                Menu = new[]
                {
                    new HwpMenuSpec { Kind = HwpMenuKind.Heading, Label = "도형은 이 앱에서 넣을 수 없어요" },
                    new HwpMenuSpec { Label = "한글에서 열기", Args = """{"command":"hwp.file.openapp"}""", Detail = "한글의 입력 > 도형에서 넣어요" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.insert.chart",
                Label = "차트",
                Class = RibbonCommandClass.External,
                Action = "openOriginalApp",
                Reason = "차트는 한글 파일에 저장되지 않아서 이 앱에서는 넣을 수 없어요. 한글에서 넣어 주세요.",
                Menu = new[]
                {
                    new HwpMenuSpec { Kind = HwpMenuKind.Heading, Label = "차트는 이 앱에서 넣을 수 없어요" },
                    new HwpMenuSpec { Label = "한글에서 열기", Args = """{"command":"hwp.file.openapp"}""", Detail = "한글의 입력 > 차트에서 넣어요" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.insert.symbols",
                Label = "문자표",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_text",
                Args = """{"ops":[{"op":"insert","id":"{id}","offset":"{offset}","text":"{char}"}]}""",
                Dialog = new WordDialogSpec("문자표", new[]
                {
                    new WordDialogField("set", "종류", "choice", "\"일반 기호\"", new string[] { "문장 부호", "일반 기호", "수학 기호", "단위", "원 숫자·괄호", "로마 숫자", "괘선", "한글 자모", "위·아래 첨자", "분수" }, Array.Empty<string>()),
                    new WordDialogField("char", "넣을 문자", "char", null, Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.find.find",
                Label = "찾기",
                Class = RibbonCommandClass.View,
                Action = "openFind",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "찾기", Detail = "찾을 글을 써 넣으면 종이에서 바로 표시해요 (Ctrl+F)", Shortcut = "Ctrl+F" },
                    new HwpMenuSpec { Label = "찾아 바꾸기", Args = """{"command":"hwp.edit.find.replace"}""", Detail = "글을 찾아 한꺼번에 바꿔요 (Ctrl+F2)", Shortcut = "Ctrl+F2" },
                    new HwpMenuSpec { Label = "찾아가기", Args = """{"command":"hwp.status.page"}""", Detail = "쪽이나 줄로 가요 (Alt+G)", Shortcut = "Alt+G" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.edit.find.replace",
                Label = "찾아 바꾸기",
                Class = RibbonCommandClass.Tool,
                Tool = "replace_text",
                Args = """{"find":"{find}","replace":"{replace}","match_case":"{match_case}","whole_word":"{whole_word}","regex":"{regex}","scope":"{scope}"}""",
                Dialog = new WordDialogSpec("찾아 바꾸기", new[]
                {
                    new WordDialogField("find", "찾을 내용", "text", "\"{selection or last search}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("replace", "바꿀 내용", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("match_case", "대소문자 구분", "bool", "false", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("whole_word", "낱말 단위로", "bool", "false", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("regex", "정규식", "bool", "false", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("scope", "찾는 범위", "choice", "\"document\"", new string[] { "document", "selection" }, new string[] { "문서 전체", "고른 부분" }),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.screen.outline",
                Label = "쪽 윤곽",
                Class = RibbonCommandClass.View,
                Action = "togglePageOutline",
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.screen.fullscreen",
                Label = "전체 화면",
                Class = RibbonCommandClass.View,
                Action = "toggleFullScreen",
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.zoom.fitpage",
                Label = "쪽 맞춤",
                Class = RibbonCommandClass.View,
                Action = "fitPage",
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.zoom.fitwidth",
                Label = "폭 맞춤",
                Class = RibbonCommandClass.View,
                Action = "fitWidth",
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.zoom.multi",
                Label = "여러 쪽",
                Class = RibbonCommandClass.View,
                Action = "toggleMulti",
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.zoom.100",
                Label = "100%",
                Class = RibbonCommandClass.View,
                Action = "setZoom",
                Args = """{"zoom":100}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.marks.codes",
                Label = "조판 부호",
                Class = RibbonCommandClass.View,
                Action = "toggleCodes",
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.marks.paramarks",
                Label = "문단 부호",
                Class = RibbonCommandClass.View,
                Action = "toggleParaMarks",
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.marks.transparent",
                Label = "투명 선",
                Class = RibbonCommandClass.View,
                Action = "toggleTransparentLines",
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.panes.pages",
                Label = "쪽 모양 보기",
                Class = RibbonCommandClass.View,
                Action = "showPane",
                Args = """{"pane":"pages"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.panes.outline",
                Label = "개요 보기",
                Class = RibbonCommandClass.View,
                Action = "showPane",
                Args = """{"pane":"outline"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.rulers.horizontal",
                Label = "가로 눈금자",
                Class = RibbonCommandClass.View,
                Action = "toggleRuler",
            },
            new HwpCommandSpec
            {
                Id = "hwp.view.rulers.vertical",
                Label = "세로 눈금자",
                Class = RibbonCommandClass.View,
                Action = "toggleVRuler",
            },
            new HwpCommandSpec
            {
                Id = "hwp.insert.objects.textbox",
                Label = "글상자",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_table",
                Args = """{"after":"{caret block id or end}","rows":[[""]],"header":false,"style":"grid","align":"left"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.insert.objects.wordart",
                Label = "글맵시",
                Class = RibbonCommandClass.External,
                Action = "openOriginalApp",
                Reason = "글맵시는 한글 파일에 저장되지 않아서 이 앱에서는 넣을 수 없어요. 한글에서 넣어 주세요.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.insert.objects.equation",
                Label = "수식",
                Class = RibbonCommandClass.External,
                Action = "openOriginalApp",
                Reason = "수식은 한글 파일에 저장되지 않아서 이 앱에서는 넣을 수 없어요. 한글에서 넣어 주세요.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.insert.field.clickhere",
                Label = "누름틀",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_form_field",
                Args = """{"action":"insert","at":"{caret}","name":"{name}","hint":"{hint}","memo":"{memo}"}""",
                Reason = "누름틀은 아직 한글 파일에 저장되지 않아요. 한글에서 넣어 주세요.",
                Gates = new[] { "ClickHere" },
                Dialog = new WordDialogSpec("누름틀 넣기", new[]
                {
                    new WordDialogField("name", "필드 이름", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("hint", "안내문", "text", "\"여기에 입력하세요\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("memo", "설명(메모)", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.insert.refs.footnote",
                Label = "각주",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_reference",
                Args = """{"kind":"footnote","at":"{caret}","note":{"text":"{text}"}}""",
                Reason = "각주는 아직 한글 파일에 저장되지 않아요. 한글에서 넣어 주세요.",
                Gates = new[] { "Notes" },
                Dialog = new WordDialogSpec("각주 내용", new[]
                {
                    new WordDialogField("text", "내용", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.insert.refs.endnote",
                Label = "미주",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_reference",
                Args = """{"kind":"endnote","at":"{caret}","note":{"text":"{text}"}}""",
                Reason = "미주는 아직 한글 파일에 저장되지 않아요. 한글에서 넣어 주세요.",
                Gates = new[] { "Notes" },
                Dialog = new WordDialogSpec("미주 내용", new[]
                {
                    new WordDialogField("text", "내용", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.insert.refs.memo",
                Label = "메모",
                Class = RibbonCommandClass.External,
                Action = "openOriginalApp",
                Reason = "메모는 한글 파일에 저장되지 않아서 이 앱에서는 달 수 없어요. 한글에서 달아 주세요.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.insert.refs.bookmark",
                Label = "책갈피",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_reference",
                Args = """{"kind":"bookmark","at":"{caret}","bookmark":{"name":"{name}"}}""",
                Reason = "책갈피는 아직 한글 파일에 저장되지 않아요. 한글에서 넣어 주세요.",
                Gates = new[] { "Bookmarks" },
                Dialog = new WordDialogSpec("책갈피", new[]
                {
                    new WordDialogField("name", "책갈피 이름", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.insert.refs.hyperlink",
                Label = "하이퍼링크",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_reference",
                Args = """{"kind":"hyperlink","at":"{targets or caret}","hyperlink":{"url":"{target}","text":"{text}"}}""",
                Reason = "하이퍼링크는 아직 한글 파일에 저장되지 않아요. 한글에서 넣어 주세요.",
                Gates = new[] { "Hyperlinks" },
                Dialog = new WordDialogSpec("하이퍼링크", new[]
                {
                    new WordDialogField("kind", "연결할 곳", "choice", "\"url\"", new string[] { "url", "email", "bookmark" }, new string[] { "웹 주소", "전자 메일", "책갈피" }),
                    new WordDialogField("target", "주소 / 책갈피", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("text", "보일 글", "text", "\"{selection}\"", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.insert.helper.hanja",
                Label = "한자 입력",
                Class = RibbonCommandClass.Assistant,
                Action = "askAssistant",
                Prompt = "고른 낱말(‘{선택한 글}’)을 문맥에 맞는 한자로 바꿔 줘. 뜻이 여러 가지면 어떤 뜻으로 골랐는지 알려 줘.",
                SelectionPrompt = "고른 낱말(‘{선택한 글}’)을 문맥에 맞는 한자로 바꿔 줘. 뜻이 여러 가지면 어떤 뜻으로 골랐는지 알려 줘.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.undo",
                Label = "되돌리기",
                Class = RibbonCommandClass.File,
                Action = "undo",
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.redo",
                Label = "다시 실행",
                Class = RibbonCommandClass.File,
                Action = "redo",
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.font",
                Label = "글꼴",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","font":"{value}"}""",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "함초롬바탕", Args = """{"font":"함초롬바탕"}""", Font = "함초롬바탕" },
                    new HwpMenuSpec { Label = "함초롬돋움", Args = """{"font":"함초롬돋움"}""", Font = "함초롬돋움" },
                    new HwpMenuSpec { Label = "맑은 고딕", Args = """{"font":"맑은 고딕"}""", Font = "맑은 고딕" },
                    new HwpMenuSpec { Label = "바탕", Args = """{"font":"바탕"}""", Font = "바탕" },
                    new HwpMenuSpec { Label = "돋움", Args = """{"font":"돋움"}""", Font = "돋움" },
                    new HwpMenuSpec { Label = "굴림", Args = """{"font":"굴림"}""", Font = "굴림" },
                    new HwpMenuSpec { Label = "궁서", Args = """{"font":"궁서"}""", Font = "궁서" },
                    new HwpMenuSpec { Label = "나눔고딕", Args = """{"font":"나눔고딕"}""", Font = "나눔고딕" },
                    new HwpMenuSpec { Label = "나눔명조", Args = """{"font":"나눔명조"}""", Font = "나눔명조" },
                    new HwpMenuSpec { Label = "HY견고딕", Args = """{"font":"HY견고딕"}""", Font = "HY견고딕" },
                    new HwpMenuSpec { Label = "HY헤드라인M", Args = """{"font":"HY헤드라인M"}""", Font = "HY헤드라인M" },
                    new HwpMenuSpec { Label = "Arial", Args = """{"font":"Arial"}""", Font = "Arial" },
                    new HwpMenuSpec { Label = "Times New Roman", Args = """{"font":"Times New Roman"}""", Font = "Times New Roman" },
                    new HwpMenuSpec { Label = "Calibri", Args = """{"font":"Calibri"}""", Font = "Calibri" },
                    new HwpMenuSpec { Label = "Consolas", Args = """{"font":"Consolas"}""", Font = "Consolas" },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "직접 입력…", Args = """{"dialog":"fontName"}""", Detail = "글꼴 이름을 써 넣어요. 깔려 있지 않은 글꼴은 대신 쓸 글꼴로 보여요" },
                },
                Dialog = new WordDialogSpec("글꼴 이름", new[]
                {
                    new WordDialogField("value", "글꼴", "text", "\"{cur.font}\"", new string[] { "함초롬바탕", "함초롬돋움", "맑은 고딕", "바탕", "돋움", "굴림", "궁서", "나눔고딕", "나눔명조", "HY견고딕", "HY헤드라인M", "Arial", "Times New Roman", "Calibri", "Consolas" }, Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.size",
                Label = "글자 크기",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","size":"{value}"}""",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "8 pt", Args = """{"size":8.0}""" },
                    new HwpMenuSpec { Label = "9 pt", Args = """{"size":9.0}""" },
                    new HwpMenuSpec { Label = "10 pt", Args = """{"size":10.0}""" },
                    new HwpMenuSpec { Label = "10.5 pt", Args = """{"size":10.5}""" },
                    new HwpMenuSpec { Label = "11 pt", Args = """{"size":11.0}""" },
                    new HwpMenuSpec { Label = "12 pt", Args = """{"size":12.0}""" },
                    new HwpMenuSpec { Label = "13 pt", Args = """{"size":13.0}""" },
                    new HwpMenuSpec { Label = "14 pt", Args = """{"size":14.0}""" },
                    new HwpMenuSpec { Label = "15 pt", Args = """{"size":15.0}""" },
                    new HwpMenuSpec { Label = "16 pt", Args = """{"size":16.0}""" },
                    new HwpMenuSpec { Label = "18 pt", Args = """{"size":18.0}""" },
                    new HwpMenuSpec { Label = "20 pt", Args = """{"size":20.0}""" },
                    new HwpMenuSpec { Label = "22 pt", Args = """{"size":22.0}""" },
                    new HwpMenuSpec { Label = "24 pt", Args = """{"size":24.0}""" },
                    new HwpMenuSpec { Label = "26 pt", Args = """{"size":26.0}""" },
                    new HwpMenuSpec { Label = "28 pt", Args = """{"size":28.0}""" },
                    new HwpMenuSpec { Label = "32 pt", Args = """{"size":32.0}""" },
                    new HwpMenuSpec { Label = "36 pt", Args = """{"size":36.0}""" },
                    new HwpMenuSpec { Label = "40 pt", Args = """{"size":40.0}""" },
                    new HwpMenuSpec { Label = "48 pt", Args = """{"size":48.0}""" },
                    new HwpMenuSpec { Label = "60 pt", Args = """{"size":60.0}""" },
                    new HwpMenuSpec { Label = "72 pt", Args = """{"size":72.0}""" },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "직접 입력…", Args = """{"dialog":"fontSize"}""", Detail = "1~400pt, 0.5 단위" },
                },
                Dialog = new WordDialogSpec("글자 크기", new[]
                {
                    new WordDialogField("value", "크기 (pt)", "number", "\"{cur.size}\"", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.bold",
                Label = "진하게",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","bold":"{toggle}"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.italic",
                Label = "기울임",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","italic":"{toggle}"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.underline",
                Label = "밑줄",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","underline":"{toggle ? single : none}"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.color",
                Label = "글자 색",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","color":"{color}"}""",
                Menu = new[]
                {
                    new HwpMenuSpec { Kind = HwpMenuKind.Swatches, Label = "글자 색", Detail = "RibbonPalette.Standard 20색 + 맨 위 ‘자동(검정)’", Palette = "Standard" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.alignjustify",
                Label = "양쪽 정렬",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","align":"justify"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.alignleft",
                Label = "왼쪽 정렬",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","align":"left"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.aligncenter",
                Label = "가운데 정렬",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","align":"center"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.alignright",
                Label = "오른쪽 정렬",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","align":"right"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.aligndistribute",
                Label = "배분 정렬",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","align":"distribute"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.aligndivide",
                Label = "나눔 정렬",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","align":"divide"}""",
                Reason = "문단을 골라야 나눔 정렬을 쓸 수 있어요.",
                Gates = new[] { "ParaDivide" },
            },
            new HwpCommandSpec
            {
                Id = "hwp.fmt.linespacing",
                Label = "줄 간격",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","line_spacing":{"mode":"multiple","value":"{value/160}"}}""",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "100 %", Args = """{"value":100}""" },
                    new HwpMenuSpec { Label = "130 %", Args = """{"value":130}""" },
                    new HwpMenuSpec { Label = "150 %", Args = """{"value":150}""" },
                    new HwpMenuSpec { Label = "160 %", Args = """{"value":160}""", Detail = "글자 크기 기준. 160 %가 한글의 기본이에요" },
                    new HwpMenuSpec { Label = "180 %", Args = """{"value":180}""" },
                    new HwpMenuSpec { Label = "200 %", Args = """{"value":200}""" },
                    new HwpMenuSpec { Label = "250 %", Args = """{"value":250}""" },
                    new HwpMenuSpec { Label = "300 %", Args = """{"value":300}""" },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "직접 입력…", Args = """{"dialog":"lineSpacing"}""", Detail = "50~500 %" },
                    new HwpMenuSpec { Label = "고정 값 (pt)…", Args = """{"dialog":"lineSpacing","mode":"fixed"}""", Detail = "줄 간격을 pt로 고정해요", Gates = new[] { "ParaFixedLine" }, Reason = "고정 값 줄 간격은 한글 파일에 저장돼요. 이 대화상자는 아직 이 앱에서 열 수 없어요." },
                },
                Dialog = new WordDialogSpec("줄 간격", new[]
                {
                    new WordDialogField("mode", "종류", "choice", "\"percent\"", new string[] { "percent", "fixed", "at_least" }, new string[] { "글자에 따라 (%)", "고정 값 (pt)", "최소 (pt)" }),
                    new WordDialogField("value", "값", "number", "\"{cur.lineSpacingPercent}\"", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.char.shade",
                Label = "형광펜",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","highlight":"{color}"}""",
                Reason = "형광펜은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요. 저장하면 사라지거든요.",
                Gates = new[] { "CharShade" },
                Menu = new[]
                {
                    new HwpMenuSpec { Kind = HwpMenuKind.Swatches, Label = "형광펜 색", Detail = "RibbonPalette.Highlight 15색 + 맨 위 ‘없음’", Palette = "Highlight" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.char.border",
                Label = "글자 테두리",
                Class = RibbonCommandClass.External,
                Action = "openOriginalApp",
                Reason = "글자 테두리는 한글 파일에 저장되지 않아서 이 앱에서는 쓸 수 없어요. 한글에서 해 주세요.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.char.spacingnarrow",
                Label = "자간 좁게",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","spacing_pt":"{cur.spacingPt-cur.size*0.05}"}""",
                Reason = "자간은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요. 저장하면 사라지거든요.",
                Gates = new[] { "CharSpacing" },
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.char.spacingwiden",
                Label = "자간 넓게",
                Class = RibbonCommandClass.Tool,
                Tool = "format_text",
                Args = """{"targets":"{targets}","spacing_pt":"{cur.spacingPt+cur.size*0.05}"}""",
                Reason = "자간은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요. 저장하면 사라지거든요.",
                Gates = new[] { "CharSpacing" },
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.para.indentless",
                Label = "왼쪽 여백 줄이기",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","indent_step":-1}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.para.indentmore",
                Label = "왼쪽 여백 늘리기",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","indent_step":1}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.para.firstin",
                Label = "첫 줄 들여쓰기",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","first_line_cm":"{cur.firstLineCm+0.35}"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.para.firstout",
                Label = "첫 줄 내어쓰기",
                Class = RibbonCommandClass.Tool,
                Tool = "format_paragraph",
                Args = """{"ids":"{ids}","hanging_cm":"{cur.hangingCm+0.35}"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.para.dropcap",
                Label = "문단 첫 글자 장식",
                Class = RibbonCommandClass.External,
                Action = "openOriginalApp",
                Reason = "문단 첫 글자 장식은 한글 파일에 저장되지 않아서 이 앱에서는 쓸 수 없어요. 한글에서 해 주세요.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.outline.outline",
                Label = "개요",
                Class = RibbonCommandClass.Tool,
                Tool = "set_list",
                Args = """{"ids":"{ids}","type":"multilevel","multilevel_preset":"{value}"}""",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "없음", Args = """{"type":"none"}""", Detail = "개요 번호를 없애요" },
                    new HwpMenuSpec { Label = "공문서 (1. 가. 1) 가) (1) (가) ① ㉮)", Args = """{"type":"multilevel","multilevel_preset":"official"}""", Detail = "정부 공문서 항목 부호 순서" },
                    new HwpMenuSpec { Label = "1. 가. 1) 가)", Args = """{"type":"multilevel","multilevel_preset":"outline"}""", Detail = "네 단계까지" },
                    new HwpMenuSpec { Label = "1. 1.1 1.1.1", Args = """{"type":"multilevel","multilevel_preset":"legal"}""", Detail = "숫자만 이어 붙이는 개요" },
                    new HwpMenuSpec { Label = "I. A. 1.", Args = """{"type":"multilevel","multilevel_preset":"roman"}""", Detail = "로마 숫자, 영문, 숫자" },
                    new HwpMenuSpec { Label = "■ □ ●", Args = """{"type":"multilevel","multilevel_preset":"square"}""", Detail = "네모, 동그라미 글머리표" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.bullets.bullets",
                Label = "글머리표",
                Class = RibbonCommandClass.Tool,
                Tool = "set_list",
                Args = """{"ids":"{ids}","type":"bullet","bullet":"{value}"}""",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "없음", Args = """{"type":"none"}""", Detail = "글머리표를 없애요" },
                    new HwpMenuSpec { Label = "●", Args = """{"type":"bullet","bullet":"●"}""" },
                    new HwpMenuSpec { Label = "○", Args = """{"type":"bullet","bullet":"○"}""" },
                    new HwpMenuSpec { Label = "■", Args = """{"type":"bullet","bullet":"■"}""" },
                    new HwpMenuSpec { Label = "□", Args = """{"type":"bullet","bullet":"□"}""" },
                    new HwpMenuSpec { Label = "◆", Args = """{"type":"bullet","bullet":"◆"}""" },
                    new HwpMenuSpec { Label = "◇", Args = """{"type":"bullet","bullet":"◇"}""" },
                    new HwpMenuSpec { Label = "▶", Args = """{"type":"bullet","bullet":"▶"}""" },
                    new HwpMenuSpec { Label = "※", Args = """{"type":"bullet","bullet":"※"}""" },
                    new HwpMenuSpec { Label = "·", Args = """{"type":"bullet","bullet":"·"}""" },
                    new HwpMenuSpec { Label = "-", Args = """{"type":"bullet","bullet":"-"}""" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.bullets.numbers",
                Label = "문단 번호",
                Class = RibbonCommandClass.Tool,
                Tool = "set_list",
                Args = """{"ids":"{ids}","type":"number","number_format":"{value}"}""",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "없음", Args = """{"type":"none"}""", Detail = "번호를 없애요" },
                    new HwpMenuSpec { Label = "1. 2. 3.", Args = """{"type":"number","number_format":"decimal"}""" },
                    new HwpMenuSpec { Label = "1) 2) 3)", Args = """{"type":"number","number_format":"decimal_paren"}""" },
                    new HwpMenuSpec { Label = "가. 나. 다.", Args = """{"type":"number","number_format":"ganada"}""" },
                    new HwpMenuSpec { Label = "가) 나) 다)", Args = """{"type":"number","number_format":"ganada_paren"}""" },
                    new HwpMenuSpec { Label = "① ② ③", Args = """{"type":"number","number_format":"circled"}""" },
                    new HwpMenuSpec { Label = "I. II. III.", Args = """{"type":"number","number_format":"upper_roman"}""" },
                    new HwpMenuSpec { Label = "i. ii. iii.", Args = """{"type":"number","number_format":"lower_roman"}""" },
                    new HwpMenuSpec { Label = "A. B. C.", Args = """{"type":"number","number_format":"upper_alpha"}""" },
                    new HwpMenuSpec { Label = "a. b. c.", Args = """{"type":"number","number_format":"lower_alpha"}""" },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "(1) (2) (3)", Args = """{"type":"number","number_format":"decimal_enclosed_paren"}""", Detail = "Word 쪽 set_list에 이 값이 생겨야 해요", Gates = new[] { "WordListEnclosed" }, Reason = "(1) 모양은 아직 준비 중이에요. 공문서 번호를 쓰면 (1) (가) 단계까지 나와요." },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.bullets.restart",
                Label = "새 번호로 시작",
                Class = RibbonCommandClass.Tool,
                Tool = "set_list",
                Args = """{"ids":"{ids}","restart":true,"start_at":1}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.level.up",
                Label = "한 수준 증가",
                Class = RibbonCommandClass.Tool,
                Tool = "set_list",
                Args = """{"ids":"{ids}","level_delta":1}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.level.down",
                Label = "한 수준 감소",
                Class = RibbonCommandClass.Tool,
                Tool = "set_list",
                Args = """{"ids":"{ids}","level_delta":-1}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.style.add",
                Label = "스타일 추가하기",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_style",
                Args = """{"action":"define","style":"{name}","based_on":"{based_on}","next_style":"{next_style}","copy_from":{"id":"{id}"}}""",
                Reason = "스타일 이름과 바탕 스타일을 정하는 대화상자는 아직 이 앱에서 열 수 없어요.",
                Gates = new[] { "NamedStyles" },
                Dialog = new WordDialogSpec("스타일 추가하기", new[]
                {
                    new WordDialogField("name", "스타일 이름", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("based_on", "바탕 스타일", "choice", "\"바탕글\"", new string[] { "바탕글", "본문", "개요 1", "개요 2" }, Array.Empty<string>()),
                    new WordDialogField("next_style", "다음 스타일", "choice", "\"바탕글\"", new string[] { "바탕글", "본문" }, Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.format.style.update",
                Label = "현재 모양으로 바꾸기",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_style",
                Args = """{"action":"modify","style":"{cur.style}","copy_from":{"id":"{id}"}}""",
                Reason = "스타일을 고치려면 문단을 골라 주세요.",
                Gates = new[] { "NamedStyles" },
            },
            new HwpCommandSpec
            {
                Id = "hwp.page.setup.paper",
                Label = "편집 용지",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"scope":"document","size":"{size}","width_cm":"{width_mm/10}","height_cm":"{height_mm/10}","orientation":"{orientation}","margins":{"top_cm":"{(top_mm+header_mm)/10}","header_cm":"{top_mm/10}","bottom_cm":"{(bottom_mm+footer_mm)/10}","footer_cm":"{bottom_mm/10}","left_cm":"{left_mm/10}","right_cm":"{right_mm/10}"}}""",
                Dialog = new WordDialogSpec("편집 용지", new[]
                {
                    new WordDialogField("size", "용지 종류", "choice", "\"A4\"", new string[] { "A3", "A4", "A5", "B4", "B5", "Letter", "Legal", "Tabloid", "Executive", "custom" }, Array.Empty<string>()),
                    new WordDialogField("width_mm", "폭 (mm)", "number", "\"{cur.pageWidthMm}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("height_mm", "길이 (mm)", "number", "\"{cur.pageHeightMm}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("orientation", "용지 방향", "choice", "\"portrait\"", new string[] { "portrait", "landscape" }, new string[] { "세로", "가로" }),
                    new WordDialogField("top_mm", "위쪽 (mm)", "number", "\"{cur.marginTopMm}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("header_mm", "머리말 (mm)", "number", "\"{cur.marginHeaderMm}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("bottom_mm", "아래쪽 (mm)", "number", "\"{cur.marginBottomMm}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("footer_mm", "꼬리말 (mm)", "number", "\"{cur.marginFooterMm}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("left_mm", "왼쪽 (mm)", "number", "\"{cur.marginLeftMm}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("right_mm", "오른쪽 (mm)", "number", "\"{cur.marginRightMm}\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("gutter_mm", "제본 (mm)", "number", "0", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.page.look.header",
                Label = "머리말",
                Class = RibbonCommandClass.Tool,
                Tool = "set_header_footer",
                Args = """{"part":"header","variant":"{variant}","left":"{left}","center":"{center}","right":"{right}"}""",
                Reason = "머리말은 아직 한글 파일에 저장되지 않아요. 한글에서 넣어 주세요.",
                Gates = new[] { "HeaderFooter" },
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "양쪽에 머리말 넣기", Args = """{"variant":"default"}""", Detail = "모든 쪽에 같은 머리말", Gates = new[] { "HeaderFooter" }, Reason = "머리말은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "짝수 쪽에만", Args = """{"variant":"even"}""", Detail = "짝수 쪽 머리말을 따로 둬요", Gates = new[] { "HeaderFooter" }, Reason = "머리말은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "홀수 쪽에만", Args = """{"variant":"default","different_odd_even":true}""", Detail = "홀수 쪽 머리말을 따로 둬요", Gates = new[] { "HeaderFooter" }, Reason = "머리말은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "머리말 내용 고치기…", Args = """{"dialog":"headerEdit"}""", Detail = "왼쪽·가운데·오른쪽 글과 쪽 번호", Gates = new[] { "HeaderFooter" }, Reason = "머리말은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "머리말 없애기", Args = """{"remove":true}""", Gates = new[] { "HeaderFooter" }, Reason = "머리말은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                },
                Dialog = new WordDialogSpec("머리말 고치기", new[]
                {
                    new WordDialogField("left", "왼쪽", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("center", "가운데", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("right", "오른쪽", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("page_number", "쪽 번호 넣기", "choice", "\"none\"", new string[] { "none", "left", "center", "right" }, new string[] { "넣지 않음", "왼쪽", "가운데", "오른쪽" }),
                    new WordDialogField("variant", "적용할 쪽", "choice", "\"default\"", new string[] { "default", "even", "odd" }, new string[] { "양쪽", "짝수 쪽", "홀수 쪽" }),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.page.look.footer",
                Label = "꼬리말",
                Class = RibbonCommandClass.Tool,
                Tool = "set_header_footer",
                Args = """{"part":"footer","variant":"{variant}","left":"{left}","center":"{center}","right":"{right}"}""",
                Reason = "꼬리말은 아직 한글 파일에 저장되지 않아요. 한글에서 넣어 주세요.",
                Gates = new[] { "HeaderFooter" },
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "양쪽에 꼬리말 넣기", Args = """{"variant":"default"}""", Detail = "모든 쪽에 같은 꼬리말", Gates = new[] { "HeaderFooter" }, Reason = "꼬리말은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "짝수 쪽에만", Args = """{"variant":"even"}""", Detail = "짝수 쪽 꼬리말을 따로 둬요", Gates = new[] { "HeaderFooter" }, Reason = "꼬리말은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "홀수 쪽에만", Args = """{"variant":"default","different_odd_even":true}""", Detail = "홀수 쪽 꼬리말을 따로 둬요", Gates = new[] { "HeaderFooter" }, Reason = "꼬리말은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "꼬리말 내용 고치기…", Args = """{"dialog":"footerEdit"}""", Detail = "왼쪽·가운데·오른쪽 글과 쪽 번호", Gates = new[] { "HeaderFooter" }, Reason = "꼬리말은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "꼬리말 없애기", Args = """{"remove":true}""", Gates = new[] { "HeaderFooter" }, Reason = "꼬리말은 아직 한글 파일에 저장되지 않아서 쓸 수 없어요." },
                },
                Dialog = new WordDialogSpec("꼬리말 고치기", new[]
                {
                    new WordDialogField("left", "왼쪽", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("center", "가운데", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("right", "오른쪽", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("page_number", "쪽 번호 넣기", "choice", "\"none\"", new string[] { "none", "left", "center", "right" }, new string[] { "넣지 않음", "왼쪽", "가운데", "오른쪽" }),
                    new WordDialogField("variant", "적용할 쪽", "choice", "\"default\"", new string[] { "default", "even", "odd" }, new string[] { "양쪽", "짝수 쪽", "홀수 쪽" }),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.page.look.pagenumber",
                Label = "쪽 번호 매기기",
                Class = RibbonCommandClass.Tool,
                Tool = "set_header_footer",
                Args = """{"page_number":{"position":"{top|bottom}","align":"{left|center|right|outside|inside}","style":"{plain|dash}","format":"{format}","start_at":"{start_at}"}}""",
                Reason = "쪽 번호는 아직 한글 파일에 저장되지 않아요. 한글에서 매겨 주세요.",
                Gates = new[] { "HeaderFooter", "PageNumber" },
                Dialog = new WordDialogSpec("쪽 번호 매기기", new[]
                {
                    new WordDialogField("position", "번호 위치", "choice", "\"가운데 아래\"", new string[] { "없음", "왼쪽 위", "가운데 위", "오른쪽 위", "왼쪽 아래", "가운데 아래", "오른쪽 아래", "바깥쪽 위", "바깥쪽 아래", "안쪽 위", "안쪽 아래" }, Array.Empty<string>()),
                    new WordDialogField("format", "번호 모양", "choice", "\"1, 2, 3\"", new string[] { "1, 2, 3", "I, II, III", "i, ii, iii", "A, B, C", "a, b, c", "가, 나, 다" }, Array.Empty<string>()),
                    new WordDialogField("dash", "줄표 넣기 (- 1 -)", "bool", "true", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("start_at", "시작 번호", "number", "1", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.page.look.newnumber",
                Label = "새 번호로 시작",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_control",
                Args = """{"action":"restart_page_number","at":"{caret}","start_at":"{start_at}"}""",
                Reason = "쪽 번호 다시 시작은 아직 한글 파일에 저장되지 않아요. 한글에서 해 주세요.",
                Gates = new[] { "PageControl" },
                Dialog = new WordDialogSpec("새 번호로 시작", new[]
                {
                    new WordDialogField("start_at", "시작 번호", "number", "1", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.page.look.hidepage",
                Label = "현재 쪽만 감추기",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_control",
                Args = """{"action":"hide_page_items","at":"{caret}","items":["header","footer","page_number"]}""",
                Reason = "현재 쪽 감추기는 아직 한글 파일에 저장되지 않아요. 한글에서 해 주세요.",
                Gates = new[] { "PageControl" },
            },
            new HwpCommandSpec
            {
                Id = "hwp.page.look.border",
                Label = "쪽 테두리/배경",
                Class = RibbonCommandClass.Tool,
                Tool = "set_page_setup",
                Args = """{"scope":"document","page_borders":{"style":"{border}","line":"{line}","width_pt":"{width_pt}","color":"{color}"},"page_color":"{page_color}"}""",
                Reason = "쪽 테두리와 배경은 아직 한글 파일에 저장되지 않아요. 한글에서 해 주세요.",
                Gates = new[] { "PageBorder" },
                Dialog = new WordDialogSpec("쪽 테두리/배경", new[]
                {
                    new WordDialogField("border", "테두리", "choice", "\"none\"", new string[] { "none", "box" }, Array.Empty<string>()),
                    new WordDialogField("line", "선 종류", "choice", "\"single\"", new string[] { "single", "double", "dashed", "dotted" }, Array.Empty<string>()),
                    new WordDialogField("width_pt", "굵기 (pt)", "number", "0.5", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("color", "선 색", "color", "\"#000000\"", Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("page_color", "배경 색", "color", "\"none\"", new string[] { "none" }, Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.page.break.page",
                Label = "쪽 나누기",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_page",
                Args = """{"kind":"page_break","at":"{caret}"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.page.break.column",
                Label = "단 나누기",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_page",
                Args = """{"kind":"column_break","at":"{caret}"}""",
                Reason = "단 나누기는 아직 한글 파일에 저장되지 않아요. 한글에서 해 주세요.",
                Gates = new[] { "Columns" },
            },
            new HwpCommandSpec
            {
                Id = "hwp.page.section.sectionbreak",
                Label = "구역 나누기",
                Class = RibbonCommandClass.Tool,
                Tool = "insert_page",
                Args = """{"kind":"section_next_page","at":"{caret}"}""",
                Gates = new[] { "Sections" },
            },
            new HwpCommandSpec
            {
                Id = "hwp.page.section.erase",
                Label = "쪽 지우기",
                Class = RibbonCommandClass.Tool,
                Tool = "delete_paragraphs",
                Args = """{"ids":"{page blocks}"}""",
                Dialog = new WordDialogSpec("쪽 지우기", Array.Empty<WordDialogField>()),
            },
            new HwpCommandSpec
            {
                Id = "hwp.secure.doc.password",
                Label = "문서 암호 설정",
                Class = RibbonCommandClass.External,
                Action = "openOriginalApp",
                Reason = "암호를 건 한글 파일은 이 앱에서 다시 열 수 없어요. 한글에서 파일 > 문서 암호 설정으로 걸어 주세요.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.secure.doc.distribute",
                Label = "배포용 문서로 저장",
                Class = RibbonCommandClass.External,
                Action = "openOriginalApp",
                Reason = "배포용 문서는 이 앱에서 다시 열 수 없어요. 한글에서 파일 > 배포용 문서로 저장으로 만들어 주세요.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.secure.doc.privacy",
                Label = "개인 정보 보호하기",
                Class = RibbonCommandClass.Assistant,
                Action = "askAssistant",
                Prompt = "이 문서에서 주민등록번호, 전화번호, 전자 메일, 계좌번호, 주소 같은 개인 정보가 어디에 있는지 찾아서 알려 줘. 가려도 되는지 물어보고, 내가 허락하면 가운데를 *로 가려 줘.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.secure.sign.sign",
                Label = "전자 서명",
                Class = RibbonCommandClass.External,
                Action = "openOriginalApp",
                Reason = "전자 서명은 인증서가 필요해서 한글에서 해야 해요. 이 앱에서 저장한 뒤 한글에서 서명해 주세요(고친 뒤에 서명하세요).",
            },
            new HwpCommandSpec
            {
                Id = "hwp.review.proof.spell",
                Label = "맞춤법",
                Class = RibbonCommandClass.Assistant,
                Action = "askAssistant",
                Prompt = "이 문서의 맞춤법과 띄어쓰기를 검사해서 틀린 곳을 고쳐 줘. 어디를 어떻게 고쳤는지 알려 줘.",
                SelectionPrompt = "고른 부분(‘{선택한 글}’)의 맞춤법과 띄어쓰기를 검사해서 틀린 곳만 고쳐 줘. 어디를 어떻게 고쳤는지 알려 줘.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.review.proof.helper",
                Label = "맞춤법 도우미",
                Class = RibbonCommandClass.View,
                Action = "toggleSpellcheck",
            },
            new HwpCommandSpec
            {
                Id = "hwp.review.memo.show",
                Label = "메모 보이기/숨기기",
                Class = RibbonCommandClass.Disabled,
                Reason = "이 앱에서는 한글 파일의 메모를 보여 줄 수 없어요. 메모는 한글에서 확인해 주세요.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.review.track.track",
                Label = "변경 내용 추적",
                Class = RibbonCommandClass.Disabled,
                Reason = "이 앱은 한글의 변경 내용 추적을 쓰지 않고, 고친 곳을 ‘바뀐 곳’으로 보여 줘요. 되돌리려면 ‘전부 되돌리기’를 써 주세요.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.review.track.accept",
                Label = "적용",
                Class = RibbonCommandClass.Disabled,
                Reason = "이 앱에서 연 한글 파일에는 적용할 변경 내용이 없어요. 고친 곳은 ‘바뀐 곳’에서 볼 수 있어요.",
                Menu = new[]
                {
                    new HwpMenuSpec { Kind = HwpMenuKind.Heading, Label = "한글의 변경 내용 추적은 이 앱에서 쓰지 않아요" },
                    new HwpMenuSpec { Kind = HwpMenuKind.Heading, Label = "고친 곳은 아래 ‘바뀐 곳’에서 보고, ‘전부 되돌리기’로 돌려요" },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "한글에서 열기", Args = """{"command":"hwp.file.openapp"}""", Detail = "한글에서 변경 내용을 적용해요" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.review.track.reject",
                Label = "적용 안 함",
                Class = RibbonCommandClass.Disabled,
                Reason = "이 앱에서 연 한글 파일에는 되돌릴 변경 내용이 없어요. 고친 곳은 ‘전부 되돌리기’로 되돌려요.",
                Menu = new[]
                {
                    new HwpMenuSpec { Kind = HwpMenuKind.Heading, Label = "한글의 변경 내용 추적은 이 앱에서 쓰지 않아요" },
                    new HwpMenuSpec { Kind = HwpMenuKind.Heading, Label = "고친 곳은 아래 ‘바뀐 곳’에서 보고, ‘전부 되돌리기’로 돌려요" },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "한글에서 열기", Args = """{"command":"hwp.file.openapp"}""", Detail = "한글에서 변경 내용을 적용 안 함해요" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.review.track.prev",
                Label = "이전 변경",
                Class = RibbonCommandClass.View,
                Action = "moveChange",
                Args = """{"delta":-1}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.review.track.next",
                Label = "다음 변경",
                Class = RibbonCommandClass.View,
                Action = "moveChange",
                Args = """{"delta":1}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.review.compare.compare",
                Label = "문서 비교",
                Class = RibbonCommandClass.File,
                Action = "compareDocuments",
                Dialog = new WordDialogSpec("문서 비교", new[]
                {
                    new WordDialogField("other", "비교할 파일", "choice", null, Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.tool.text.dictionary",
                Label = "사전",
                Class = RibbonCommandClass.Assistant,
                Action = "askAssistant",
                Prompt = "고른 낱말(‘{선택한 글}’)의 뜻과 비슷한 말을 알려 줘. 문서는 고치지 마.",
                SelectionPrompt = "고른 낱말(‘{선택한 글}’)의 뜻과 비슷한 말을 알려 줘. 문서는 고치지 마.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.tool.text.quickfix",
                Label = "빠른 교정",
                Class = RibbonCommandClass.Assistant,
                Action = "askAssistant",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "맞춤법과 띄어쓰기 고치기", Args = """{"command":"hwp.review.proof.spell"}""", Detail = "틀린 곳을 찾아 고쳐요" },
                    new HwpMenuSpec { Label = "어색한 문장 다듬기", Args = """{"prompt":"이 문서에서 어색하거나 너무 긴 문장을 찾아 자연스럽게 다듬어 줘. 뜻은 바꾸지 말고, 고친 곳을 알려 줘."}""", Detail = "뜻은 그대로 두고 문장만 매끄럽게" },
                    new HwpMenuSpec { Label = "말투 통일하기 (해요체/합니다체)", Args = """{"prompt":"이 문서의 말투를 한 가지로 통일해 줘. 지금 가장 많이 쓰인 말투를 기준으로 하고, 어디를 고쳤는지 알려 줘."}""", Detail = "섞여 있는 말투를 하나로" },
                    new HwpMenuSpec { Label = "날짜·숫자 표기 통일하기", Args = """{"prompt":"이 문서의 날짜(예: 2026. 7. 3.)와 숫자(천 단위 쉼표 등) 표기를 한 가지로 통일해 줘. 공문서 표기 방식을 따르고, 고친 곳을 알려 줘."}""", Detail = "공문서 표기에 맞춰서" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.tool.doc.mailmerge",
                Label = "메일 머지",
                Class = RibbonCommandClass.File,
                Action = "mailMerge",
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "메일 머지 표시 달기…", Args = """{"command":"hwp.tool.doc.mailmerge.mark"}""", Detail = "문서에 {{이름}} 꼴로 자리를 만들어요" },
                    new HwpMenuSpec { Label = "메일 머지 만들기…", Args = """{"dialog":"mailMergeMake"}""", Detail = "엑셀 표의 줄마다 한 부씩 새 한글 파일을 만들어요" },
                },
                Dialog = new WordDialogSpec("메일 머지 만들기", new[]
                {
                    new WordDialogField("source", "자료 파일(엑셀)", "choice", null, Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("sheet", "시트", "choice", null, Array.Empty<string>(), Array.Empty<string>()),
                    new WordDialogField("output", "만드는 방법", "choice", "\"one\"", new string[] { "one", "many" }, new string[] { "한 파일에 쪽 나눠 모으기", "줄마다 파일 하나씩" }),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.tool.doc.mailmerge.mark",
                Label = "메일 머지 표시 달기",
                Class = RibbonCommandClass.Tool,
                Tool = "edit_text",
                Args = """{"ops":[{"op":"insert","id":"{id}","offset":"{offset}","text":"{{{name}}}"}]}""",
                Dialog = new WordDialogSpec("메일 머지 표시 달기", new[]
                {
                    new WordDialogField("name", "필드 이름", "text", "\"\"", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.tool.doc.toc",
                Label = "차례/색인",
                Class = RibbonCommandClass.Tool,
                Tool = "manage_toc",
                Args = """{"action":"insert","kind":"contents","at":"{caret}","contents":{"style":"manual","right_align":true,"tab_leader":"dot","hyperlinks":false}}""",
                Reason = "차례는 아직 한글 파일에 알맞게 저장되지 않아요. 한글에서 만들어 주세요.",
                Gates = new[] { "StaticToc" },
                Menu = new[]
                {
                    new HwpMenuSpec { Label = "제목 차례 만들기", Args = """{"action":"insert","kind":"contents"}""", Detail = "개요·제목 문단으로 차례를 만들어요", Gates = new[] { "StaticToc" }, Reason = "차례는 아직 한글 파일에 알맞게 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "표 차례 만들기", Args = """{"action":"insert","kind":"figures","figures":{"label":"표"}}""", Detail = "‘표 1.’ 같은 설명 문단으로", Gates = new[] { "StaticToc" }, Reason = "차례는 아직 한글 파일에 알맞게 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "그림 차례 만들기", Args = """{"action":"insert","kind":"figures","figures":{"label":"그림"}}""", Detail = "‘그림 1.’ 같은 설명 문단으로", Gates = new[] { "StaticToc" }, Reason = "차례는 아직 한글 파일에 알맞게 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "차례 새로 고침", Args = """{"action":"update","kind":"contents"}""", Detail = "쪽 번호와 제목을 다시 맞춰요", Gates = new[] { "StaticToc" }, Reason = "차례는 아직 한글 파일에 알맞게 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Label = "차례 지우기", Args = """{"action":"remove","kind":"contents"}""", Gates = new[] { "StaticToc" }, Reason = "차례는 아직 한글 파일에 알맞게 저장되지 않아서 쓸 수 없어요." },
                    new HwpMenuSpec { Kind = HwpMenuKind.Separator },
                    new HwpMenuSpec { Label = "색인 만들기", Args = """{"command":"hwp.file.openapp"}""", Detail = "색인은 한글에서 만들어요" },
                    new HwpMenuSpec { Label = "색인 표시 달기", Args = """{"command":"hwp.file.openapp"}""", Detail = "색인은 한글에서 만들어요" },
                },
            },
            new HwpCommandSpec
            {
                Id = "hwp.tool.settings.options",
                Label = "환경 설정",
                Class = RibbonCommandClass.View,
                Action = "openSettings",
                Args = """{"section":"hwp"}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.file.save",
                Label = "저장하기",
                Class = RibbonCommandClass.File,
                Action = "save",
            },
            new HwpCommandSpec
            {
                Id = "hwp.file.saveas",
                Label = "다른 이름으로 저장하기",
                Class = RibbonCommandClass.File,
                Action = "saveAs",
            },
            new HwpCommandSpec
            {
                Id = "hwp.file.pdf",
                Label = "PDF로 저장하기",
                Class = RibbonCommandClass.File,
                Action = "exportPdf",
            },
            new HwpCommandSpec
            {
                Id = "hwp.file.print",
                Label = "인쇄",
                Class = RibbonCommandClass.File,
                Action = "print",
            },
            new HwpCommandSpec
            {
                Id = "hwp.file.preview",
                Label = "미리 보기",
                Class = RibbonCommandClass.View,
                Action = "togglePreview",
            },
            new HwpCommandSpec
            {
                Id = "hwp.file.info",
                Label = "문서 정보",
                Class = RibbonCommandClass.File,
                Action = "showInfo",
            },
            new HwpCommandSpec
            {
                Id = "hwp.file.openapp",
                Label = "한글에서 열기",
                Class = RibbonCommandClass.External,
                Action = "openOriginalApp",
                Reason = "저장된 파일이 아직 없어서 열 수 없어요.",
            },
            new HwpCommandSpec
            {
                Id = "hwp.trailing.collapse",
                Label = "도구 상자 접기",
                Class = RibbonCommandClass.View,
                Action = "toggleRibbon",
            },
            new HwpCommandSpec
            {
                Id = "hwp.status.page",
                Label = "쪽 찾아가기",
                Class = RibbonCommandClass.View,
                Action = "goToPage",
                Dialog = new WordDialogSpec("찾아가기", new[]
                {
                    new WordDialogField("kind", "이동 종류", "choice", "\"page\"", new string[] { "page", "line" }, new string[] { "쪽", "줄" }),
                    new WordDialogField("number", "번호", "number", "1", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.status.line",
                Label = "줄 찾아가기",
                Class = RibbonCommandClass.View,
                Action = "goToLine",
                Dialog = new WordDialogSpec("찾아가기", new[]
                {
                    new WordDialogField("kind", "이동 종류", "choice", "\"page\"", new string[] { "page", "line" }, new string[] { "쪽", "줄" }),
                    new WordDialogField("number", "번호", "number", "1", Array.Empty<string>(), Array.Empty<string>()),
                }),
            },
            new HwpCommandSpec
            {
                Id = "hwp.status.insert",
                Label = "삽입/수정 바꾸기",
                Class = RibbonCommandClass.View,
                Action = "toggleOverwrite",
            },
            new HwpCommandSpec
            {
                Id = "hwp.status.prevchange",
                Label = "이전 바뀐 곳",
                Class = RibbonCommandClass.View,
                Action = "moveChange",
                Args = """{"delta":-1}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.status.nextchange",
                Label = "다음 바뀐 곳",
                Class = RibbonCommandClass.View,
                Action = "moveChange",
                Args = """{"delta":1}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.status.revertall",
                Label = "전부 되돌리기",
                Class = RibbonCommandClass.File,
                Action = "revertAll",
            },
            new HwpCommandSpec
            {
                Id = "hwp.status.zoomout",
                Label = "축소",
                Class = RibbonCommandClass.View,
                Action = "zoomBy",
                Args = """{"delta":-10}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.status.zoomin",
                Label = "확대",
                Class = RibbonCommandClass.View,
                Action = "zoomBy",
                Args = """{"delta":10}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.status.zoomslider",
                Label = "화면 확대/축소",
                Class = RibbonCommandClass.View,
                Action = "setZoom",
            },
            new HwpCommandSpec
            {
                Id = "hwp.status.zoompercent",
                Label = "화면 확대/축소",
                Class = RibbonCommandClass.View,
                Action = "setZoom",
                Args = """{"zoom":100}""",
            },
            new HwpCommandSpec
            {
                Id = "hwp.nav.goto.page",
                Label = "쪽 모양 보기 썸네일",
                Class = RibbonCommandClass.View,
                Action = "goToPage",
            },
            new HwpCommandSpec
            {
                Id = "hwp.nav.goto.heading",
                Label = "개요 보기 줄",
                Class = RibbonCommandClass.View,
                Action = "goToHeading",
            },
        };
    }
}
