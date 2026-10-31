using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocuHelperEditor.State.Ribbon;
using static DocuHelperEditor.State.Ribbon.RibbonBuilder;

namespace DocuHelperEditor.Components.Dev
{
    public static class RibbonPlaygroundCatalog
    {
        public static IReadOnlyList<RibbonTab> TabsFor(DocuType format, int level)
        {
            switch (format)
            {
                case DocuType.Excel:
                    return new[] { ExcelHome(level), ExcelView(level), RouterTab() };
                case DocuType.Ppt:
                    return new[] { PptHome(), PptTransition(), PptView(), RouterTab() };
                case DocuType.Hwp:
                    return new[] { HwpEdit(), HwpView(), RouterTab() };
                default:
                    return new[] { WordHome(), WordInsert(), WordLayout(), WordView(), RouterTab() };
            }
        }

        public static IReadOnlyList<RibbonFileItem> FileItemsFor(DocuType format)
        {
            return BaseFileItems(format)
                .Concat(new[]
                {
                    FileItem("rt.file.save", "저장 (라우터)", "Ctrl+S", separated: true),
                    FileItem("rt.off.always", "꺼 둔 항목 (라우터)"),
                    FileItem("rt.file.locked", "잠겨도 되는 항목 (라우터)"),
                })
                .ToArray();
        }

        private static IReadOnlyList<RibbonFileItem> BaseFileItems(DocuType format)
        {
            switch (format)
            {
                case DocuType.Excel:
                    return new[]
                    {
                        FileItem("save", "저장", "Ctrl+S"),
                        FileItem("save-as", "다른 이름으로 저장"),
                        FileItem("pdf", "PDF로 저장"),
                        FileItem("print", "인쇄", "Ctrl+P"),
                        FileItem("info", "통합 문서 정보"),
                        FileItem("open-app", "Excel에서 열기", separated: true),
                    };
                case DocuType.Ppt:
                    return new[]
                    {
                        FileItem("save", "저장", "Ctrl+S"),
                        FileItem("save-as", "다른 이름으로 저장"),
                        FileItem("pdf", "PDF로 저장"),
                        FileItem("print", "인쇄", "Ctrl+P"),
                        FileItem("handout", "유인물 인쇄"),
                        FileItem("open-app", "PowerPoint에서 열기", separated: true),
                    };
                case DocuType.Hwp:
                    return new[]
                    {
                        FileItem("save", "저장하기", "Alt+S"),
                        FileItem("save-as", "다른 이름으로 저장하기", "Alt+V"),
                        FileItem("pdf", "PDF로 저장하기"),
                        FileItem("print", "인쇄", "Alt+P"),
                        FileItem("preview", "미리 보기"),
                        FileItem("info", "문서 정보", "Ctrl+Q,I"),
                        FileItem("open-app", "한글에서 열기", separated: true),
                    };
                default:
                    return new[]
                    {
                        FileItem("save", "저장", "Ctrl+S"),
                        FileItem("save-as", "다른 이름으로 저장"),
                        FileItem("pdf", "PDF로 저장"),
                        FileItem("print", "인쇄", "Ctrl+P"),
                        FileItem("info", "문서 정보"),
                        FileItem("open-app", "Word에서 열기", separated: true),
                    };
            }
        }

        public static IReadOnlyList<RibbonItem> HwpFormatRow() => new[]
        {
            I("undo", "되돌리기", "undo", key: "Ctrl+Z"),
            I("redo", "다시 실행", "redo", key: "Ctrl+Shift+Z"),
            Sep(),
            D("hwp-style", "스타일", "바탕글", 92, key: "F6"),
            D("hwp-font", "글꼴", "함초롬바탕", 120, ml: 2),
            D("hwp-size", "글자 크기", "10.0 pt", 76, ml: 2),
            Sep(),
            I("bold", "진하게", "bold", key: "Alt+L", toggle: true, sw: 2.4),
            I("italic", "기울임", "italic", toggle: true),
            I("under", "밑줄", "under", toggle: true),
            I("fcolor", "글자 색", "fcolor", bar: "1C2A48"),
            Sep(),
            I("ajust", "양쪽 정렬", "ajust", toggle: true),
            I("aleft", "왼쪽 정렬", "aleft", toggle: true),
            I("acenter", "가운데 정렬", "acenter", toggle: true),
            I("aright", "오른쪽 정렬", "aright", toggle: true),
            I("adist", "배분 정렬", "adist", toggle: true),
            I("adiv", "나눔 정렬", "adiv", toggle: true),
            Sep(),
            D("hwp-spacing", "줄 간격", "160 %", 84),
        };

        private static RibbonTab RouterTab() => Tab("router", "라우터",
            Grid("도구", L("rt.tool.write", "셀에 쓰기", "table"), L("rt.tool.refuse", "거절 보기", "warn"), L("rt.tool.later", "나중 도구", "wand"), S("rt.tool.bad", "숫자 아님", "calc")),
            Grid("화면", L("rt.view.flip", "펼쳐 보기", "grid", toggle: true)),
            Grid("클립보드", S("rt.clip.copy", "글 복사", "copy"), S("rt.clip.html", "서식 복사", "brush"), S("rt.clip.image", "그림 복사", "image"), S("rt.clip.paste", "읽어 보기", "paste")),
            Grid("도우미", L("rt.ai.spell", "맞춤법 부탁", "spell")),
            Grid("바깥", L("rt.ext.open", "원본 열기", "external")),
            Grid("파일", S("rt.file.save", "저장", "check"), S("rt.file.undo", "실행 취소", "undo"), S("rt.file.redo", "다시 실행", "redo"), S("rt.file.print", "인쇄", "view-print"), S("rt.file.pdf", "PDF", "view-page"), S("rt.file.locked", "잠겨도 됨", "accept")),
            Grid("막힘", L("rt.off.always", "항상 꺼짐", "lock"), L("rt.off.select", "고른 것 필요", "select"), L("rt.menu.reason", "메뉴 사유", "list", dd: true)),
            Grid("대화상자", S("rt.dialog.image", "그림 고르기", "image"), S("rt.dialog.audio", "소리 고르기", "speak")));

        private static RibbonTab WordHome() => Tab("home", "홈",
            Grid("클립보드", L("paste", "붙여넣기", "paste", dd: true), I("cut", "잘라내기", "cut", key: "Ctrl+X"), I("copy", "복사", "copy", key: "Ctrl+C"), I("brush", "서식 복사", "brush")),
            Rows("글꼴",
                Row(D("font", "글꼴", "맑은 고딕", 124), D("size", "글꼴 크기", "11", 60), I("grow", "글꼴 크기 크게", "grow"), I("shrink", "글꼴 크기 작게", "shrink")),
                Row(I("bold", "굵게", "bold", key: "Ctrl+B", toggle: true), I("italic", "기울임꼴", "italic", key: "Ctrl+I", toggle: true), I("under", "밑줄", "under", key: "Ctrl+U", toggle: true), I("strike", "취소선", "strike", toggle: true), I("fcolor", "글꼴 색", "fcolor", bar: "C0392B"), I("hilite", "텍스트 강조 색", "hilite", bar: "FFE35A"))),
            Rows("단락",
                Row(I("bullets", "글머리 기호", "bullets", toggle: true), I("numbers", "번호 매기기", "numbers", toggle: true), I("multi", "다단계 목록", "multi"), I("outdent", "내어쓰기", "outdent"), I("indent", "들여쓰기", "indent")),
                Row(I("aleft", "왼쪽 맞춤", "aleft", toggle: true), I("acenter", "가운데 맞춤", "acenter", toggle: true), I("aright", "오른쪽 맞춤", "aright", toggle: true), I("ajust", "양쪽 맞춤", "ajust", toggle: true), I("lspace", "줄 및 단락 간격", "lspace"))),
            Grid("스타일", W("st-normal", "표준", 84), W("st-h1", "제목 1", 84, "font-size: 15px; font-weight: 700"), W("st-h2", "제목 2", 84, "font-weight: 700"), W("st-none", "간격 없음", 84)),
            Grid("편집", S("search", "찾기", "search", dd: true), S("replace", "바꾸기", "replace")));

        private static RibbonTab WordInsert() => Tab("insert", "삽입",
            Grid("페이지", L("cover", "표지", "cover", dd: true), S("blank", "빈 페이지", "page"), S("pbreak", "페이지 나누기", "pbreak")),
            Grid("표", L("table", "표", "table", dd: true)),
            Grid("일러스트레이션", L("image", "그림", "image", dd: true), L("shapes", "도형", "shapes", dd: true), L("chart", "차트", "chart")),
            Grid("링크", L("link", "링크", "link", dd: true)),
            Grid("메모", L("comment", "메모", "comment")),
            Grid("머리글/바닥글", L("header", "머리글", "header", dd: true), L("footer", "바닥글", "footer", dd: true), L("pnum", "페이지 번호", "pnum", dd: true)),
            Grid("텍스트", L("textbox", "텍스트 상자", "textbox", dd: true), S("date", "날짜 및 시간", "calendar"), S("symbol", "기호", "omega", dd: true)));

        private static RibbonTab WordLayout() => Tab("layout", "레이아웃",
            Grid("페이지 설정", L("margins", "여백", "margins", dd: true), L("orient", "용지 방향", "orient", dd: true), L("size", "크기", "size", dd: true), L("cols", "단", "cols", dd: true), S("breaks", "나누기", "pbreak", dd: true), S("lines", "줄 번호", "numbers", dd: true)),
            Rows("단락",
                Row(D("ind-left", "왼쪽 들여쓰기", "왼쪽 0 cm", 124), D("ind-right", "오른쪽 들여쓰기", "오른쪽 0 cm", 136)),
                Row(D("sp-before", "단락 앞 간격", "앞 0 pt", 124), D("sp-after", "단락 뒤 간격", "뒤 8 pt", 136))),
            Grid("정렬", L("pos", "위치", "pos", dd: true), L("wrap", "텍스트 줄 바꿈", "wrap", dd: true), S("front", "앞으로 가져오기", "front", dd: true), S("back", "뒤로 보내기", "back", dd: true)));

        private static RibbonTab WordView() => Tab("view", "보기",
            Grid("보기", L("v-read", "읽기 모드", "read", toggle: true), L("v-print", "인쇄 모양", "page", toggle: true), L("v-web", "웹 모양", "web", toggle: true), S("v-outline", "개요", "multi"), S("v-draft", "초안", "draft")),
            Grid("페이지 이동", L("v-vert", "세로", "vert", toggle: true), L("v-side", "나란히", "side", toggle: true)),
            Grid("표시", C("ruler", "눈금자"), C("gridlines", "눈금선"), C("nav", "탐색 창")),
            Grid("확대/축소", L("zoom", "확대/축소", "zoom"), L("z100", "100%", "z100"), S("z-one", "한 페이지", "onep"), S("z-multi", "여러 페이지", "side"), S("z-width", "페이지 너비", "pwidth")));

        private static RibbonTab ExcelHome(int level)
        {
            var groups = new List<RibbonGroup>
            {
                Grid("클립보드", new[] { L("paste", "붙여넣기", "paste", dd: true, key: "Ctrl+V"), I("cut", "잘라내기", "cut", key: "Ctrl+X"), I("copy", "복사", "copy", key: "Ctrl+C") }.Concat(level >= 2 ? new[] { I("brush", "서식 복사", "brush") } : new RibbonItem[0])),
                Rows("글꼴",
                    Row(D("font", "글꼴", "맑은 고딕", 118), D("size", "글꼴 크기", "11", 56), I("grow", "글꼴 크기 크게", "grow"), I("shrink", "글꼴 크기 작게", "shrink")),
                    Row(I("bold", "굵게", "bold", key: "Ctrl+B", toggle: true), I("italic", "기울임꼴", "italic", key: "Ctrl+I", toggle: true), I("under", "밑줄", "under", key: "Ctrl+U", toggle: true), I("border", "테두리", "border"), I("fill", "채우기 색", "fill", bar: "FFE35A"), I("fcolor", "글꼴 색", "fcolor", bar: "C0392B"))),
                level >= 2
                    ? Rows("맞춤",
                        Row(I("vtop", "위쪽 맞춤", "vtop", toggle: true), I("vmid", "세로 가운데 맞춤", "vmid", toggle: true), I("vbot", "아래쪽 맞춤", "vbot", toggle: true), I("wrapt", "자동 줄 바꿈", "wrapt", toggle: true)),
                        Row(I("aleft", "왼쪽 맞춤", "aleft", toggle: true), I("acenter", "가운데 맞춤", "acenter", toggle: true), I("aright", "오른쪽 맞춤", "aright", toggle: true), I("mergec", "병합하고 가운데 맞춤", "mergec")))
                    : Grid("맞춤", L("align", "맞춤", "acenter", dd: true)),
                level >= 3
                    ? Rows("표시 형식",
                        Row(D("numfmt", "표시 형식", "숫자", 136)),
                        Row(I("won", "회계 표시 형식", glyph: "₩", dd: true, w: 56), I("percent", "백분율 스타일", glyph: "%", key: "Ctrl+Shift+%"), I("comma", "쉼표 스타일", glyph: ",")))
                    : Grid("표시 형식", L("numfmt-menu", "표시 형식", glyph: "%", dd: true)),
                level >= 3
                    ? Grid("스타일", L("cond", "조건부 서식", "cond", dd: true), L("tstyle", "표 서식", "table", dd: true), L("cstyle", "셀 스타일", "cstyle", dd: true))
                    : Grid("스타일", L("styles-menu", "스타일", "cstyle", dd: true)),
                level >= 3
                    ? Grid("셀", L("cins", "삽입", "cins", dd: true), L("cdel", "삭제", "cdel", dd: true), L("cfmt", "서식", "cfmt", dd: true))
                    : Grid("셀", L("cells-menu", "셀", "cins", dd: true)),
                Grid("편집", L("sum", "자동 합계", "sigma", dd: true, key: "Alt+="), I("sortf", "정렬 및 필터", "sortf"), I("find", "찾기 및 선택", "search", key: "Ctrl+F")),
            };

            return Tab("home", "홈", groups);
        }

        private static RibbonTab ExcelView(int level) => Tab("view", "보기",
            Grid("통합 문서 보기", L("v-normal", "기본", "normal", toggle: true), L("v-break", "페이지 나누기 미리 보기", "pbview", toggle: true), L("v-layout", "페이지 레이아웃", "plview", toggle: true)),
            Grid("표시", C("ruler", "눈금자"), C("fxbar", "수식 입력줄"), C("gridlines", "눈금선"), C("headings", "머리글")),
            Grid("확대/축소", L("zoom", "확대/축소", "zoom"), L("z100", "100%", "z100"), L("zsel", "선택 영역 확대/축소", "zsel")),
            Grid("창", new[] { L("freeze", "틀 고정", "freeze", dd: true, toggle: true) }.Concat(level >= 2 ? new[] { S("newwin", "새 창", "newwin"), S("arrange", "모두 정렬", "side"), S("split", "나누기", "border"), S("hide", "숨기기", "eyeoff") } : new RibbonItem[0])));

        private static RibbonTab PptHome() => Tab("home", "홈",
            Grid("클립보드", L("paste", "붙여넣기", "paste", dd: true), I("cut", "잘라내기", "cut", key: "Ctrl+X"), I("copy", "복사", "copy", key: "Ctrl+C"), I("brush", "서식 복사", "brush")),
            Grid("슬라이드", L("newslide", "새 슬라이드", "newslide", dd: true, key: "Ctrl+M"), S("layout", "레이아웃", "layout", dd: true), S("reset", "다시 설정", "refresh")),
            Rows("글꼴",
                Row(D("font", "글꼴", "맑은 고딕", 112), D("size", "글꼴 크기", "24", 60), I("grow", "글꼴 크기 크게", "grow"), I("shrink", "글꼴 크기 작게", "shrink")),
                Row(I("bold", "굵게", "bold", key: "Ctrl+B", toggle: true), I("italic", "기울임꼴", "italic", key: "Ctrl+I", toggle: true), I("under", "밑줄", "under", key: "Ctrl+U", toggle: true), I("strike", "취소선", "strike", toggle: true), I("fcolor", "글꼴 색", "fcolor", bar: "B5462A"))),
            Rows("단락",
                Row(I("bullets", "글머리 기호", "bullets", toggle: true), I("numbers", "번호 매기기", "numbers", toggle: true), I("outdent", "목록 수준 줄임", "outdent"), I("indent", "목록 수준 늘림", "indent"), I("lspace", "줄 간격", "lspace")),
                Row(I("aleft", "텍스트 왼쪽 맞춤", "aleft", toggle: true), I("acenter", "가운데 맞춤", "acenter", toggle: true), I("aright", "텍스트 오른쪽 맞춤", "aright", toggle: true), I("ajust", "양쪽 맞춤", "ajust", toggle: true))),
            Grid("그리기", L("shapes", "도형", "shapes", dd: true), L("arrange", "정렬", "front", dd: true)));

        private static RibbonTab PptTransition() => Tab("trans", "전환",
            Grid("미리 보기", L("preview", "미리 보기", "play")),
            Grid("슬라이드 화면 전환", L("t-none", "없음", "none", toggle: true), L("t-fade", "밝기 변화", "fade", toggle: true), L("t-push", "밀어내기", "push", toggle: true), L("t-wipe", "닦아내기", "wipe", toggle: true), L("t-split", "나누기", "split", toggle: true), L("t-reveal", "나타내기", "reveal", toggle: true), L("t-options", "효과 옵션", "sliders", dd: true)),
            Rows("타이밍",
                Row(D("t-sound", "소리", "[소리 없음]", 140), D("t-duration", "기간", "기간 02.00", 116)),
                Row(S("t-all", "모두 적용", "check"), C("t-click", "마우스를 클릭할 때"))));

        private static RibbonTab PptView() => Tab("view", "보기",
            Grid("프레젠테이션 보기", L("v-normal", "기본", "normal", toggle: true), L("v-outline", "개요 보기", "outline"), L("v-sorter", "여러 슬라이드", "grid", toggle: true), L("v-notes", "슬라이드 노트", "notes"), L("v-read", "읽기용 보기", "read", toggle: true)),
            Grid("마스터 보기", L("master", "슬라이드 마스터", "master"), S("master-handout", "유인물 마스터", "master"), S("master-notes", "슬라이드 노트 마스터", "notes")),
            Grid("표시", C("ruler", "눈금자"), C("gridlines", "눈금선"), C("guides", "안내선"), C("notes", "노트")),
            Grid("확대/축소", L("zoom", "확대/축소", "zoom"), L("fit", "창에 맞춤", "fit")));

        private static RibbonTab HwpEdit() => Tab("edit", "편집",
            Grid("클립보드", L("paste", "붙이기", "paste", dd: true, key: "Ctrl+V"), I("cut", "오려 두기", "cut", key: "Ctrl+X"), I("copy", "복사하기", "copy", key: "Ctrl+C"), I("brush", "모양 복사", "brush", key: "Alt+C"), I("selall", "모두 선택", "select", key: "Ctrl+A")),
            Grid("서식", L("charfmt", "글자 모양", glyph: "가", key: "Alt+L"), L("parafmt", "문단 모양", "pilcrow", key: "Alt+T"), L("style", "스타일", "type", dd: true, key: "F6")),
            Grid("쪽", L("margins", "쪽 여백", "margins", dd: true), S("portrait", "세로", "onep", toggle: true), S("landscape", "가로", "land", toggle: true), L("cols", "단", "cols", dd: true)),
            Grid("글자 바꾸기", L("replace", "글자 바꾸기", "replace", dd: true)),
            Grid("입력", L("table", "표", "table", dd: true, key: "Ctrl+N,T"), L("image", "그림", "image", dd: true, key: "Ctrl+N,I"), L("shapes", "도형", "shapes", dd: true), S("chart", "차트", "chart"), S("symbols", "문자표", "omega", key: "Ctrl+F10")),
            Grid("찾기", L("search", "찾기", "search", dd: true, key: "Ctrl+F")));

        private static RibbonTab HwpView() => Tab("view", "보기",
            Grid("화면", L("outline", "쪽 윤곽", "outline", key: "Ctrl+G,L", toggle: true), L("fullscreen", "전체 화면", "fit", key: "Ctrl+G,Z")),
            Grid("화면 확대/축소", L("z-one", "쪽 맞춤", "onep"), L("z-width", "폭 맞춤", "pwidth"), L("z-multi", "여러 쪽", "side", toggle: true), L("z100", "100%", "z100")),
            Grid("표시/숨기기", C("codes", "조판 부호"), C("paracodes", "문단 부호"), C("lines", "투명 선")),
            Grid("작업 창", C("pane-pages", "쪽 모양 보기"), C("pane-head", "개요 보기")),
            Grid("문서 창", C("ruler", "가로 눈금자"), C("vruler", "세로 눈금자")));
    }
}
