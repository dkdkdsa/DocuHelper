using System.Collections.Generic;
using System.Linq;
using static DocuHelperEditor.State.Ribbon.RibbonBuilder;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public static class ExcelRibbonDefinition
    {
        public const int MaxLevel = 3;

        private static readonly IReadOnlyList<RibbonTab>[] s_tabs = new IReadOnlyList<RibbonTab>[MaxLevel + 1];

        private static readonly object s_lock = new object();

        public static IReadOnlyList<RibbonFileItem> FileItems { get; } = new[]
        {
            FileItem("excel.file.save", "저장", key: "Ctrl+S"),
            FileItem("excel.file.saveas", "다른 이름으로 저장"),
            FileItem("excel.file.pdf", "PDF로 저장"),
            FileItem("excel.file.print", "인쇄", key: "Ctrl+P"),
            FileItem("excel.file.info", "통합 문서 정보"),
            FileItem("excel.file.openexcel", "Excel에서 열기", separated: true),
        };

        public static IReadOnlyList<int> Breakpoints { get; } = new[] { 930, 1270 };

        public static int LevelFor(double width) => 1 + Breakpoints.Count(point => width >= point);

        public static IReadOnlyList<RibbonTab> Tabs(int level)
        {
            level = System.Math.Clamp(level, 1, MaxLevel);
            lock (s_lock)
                return s_tabs[level] ??= Build(level);
        }

        private static IReadOnlyList<RibbonTab> Build(int level) => new[]
        {
            HomeTab(level),
            InsertTab(level),
            LayoutTab(level),
            FormulaTab(level),
            DataTab(level),
            ReviewTab(level),
            ViewTab(level),
        };

        private static IEnumerable<RibbonGroup> Groups(params RibbonGroup[] groups) => groups.Where(g => g != null);

        private static RibbonGroup Pick(int level, params (int Min, RibbonGroup Group)[] variants)
            => variants.Where(v => v.Min <= level).OrderByDescending(v => v.Min).Select(v => v.Group).FirstOrDefault();

        private static RibbonTab HomeTab(int level) => Tab("home", "홈", Groups(
            Pick(level,
                (1, Grid("클립보드",
                    L("excel.home.paste", "붙여넣기", "paste", dd: true, key: "Ctrl+V"),
                    I("excel.home.cut", "잘라내기", "cut", key: "Ctrl+X"),
                    I("excel.home.copy", "복사", "copy", key: "Ctrl+C"))),
                (2, Grid("클립보드",
                    L("excel.home.paste", "붙여넣기", "paste", dd: true, key: "Ctrl+V"),
                    I("excel.home.cut", "잘라내기", "cut", key: "Ctrl+X"),
                    I("excel.home.copy", "복사", "copy", key: "Ctrl+C"),
                    I("excel.home.formatpainter", "서식 복사", "brush", toggle: true)))),
            Rows("글꼴",
                Row(D("excel.home.fontname", "글꼴", "맑은 고딕", 118), D("excel.home.fontsize", "글꼴 크기", "11", 56), I("excel.home.fontgrow", "글꼴 크기 크게", "grow"), I("excel.home.fontshrink", "글꼴 크기 작게", "shrink")),
                Row(I("excel.home.bold", "굵게", "bold", key: "Ctrl+B", toggle: true), I("excel.home.italic", "기울임꼴", "italic", key: "Ctrl+I", toggle: true), I("excel.home.underline", "밑줄", "under", key: "Ctrl+U", toggle: true), I("excel.home.border", "테두리", "border", bar: ""), I("excel.home.fillcolor", "채우기 색", "fill", bar: "FFE35A"), I("excel.home.fontcolor", "글꼴 색", "fcolor", bar: "C0392B"))),
            Pick(level,
                (1, Grid("맞춤",
                    L("excel.home.alignment.menu", "맞춤", "acenter", dd: true))),
                (2, Rows("맞춤",
                    Row(I("excel.home.valign.top", "위쪽 맞춤", "vtop", toggle: true), I("excel.home.valign.middle", "세로 가운데 맞춤", "vmid", toggle: true), I("excel.home.valign.bottom", "아래쪽 맞춤", "vbot", toggle: true), I("excel.home.wrap", "자동 줄 바꿈", "wrapt", toggle: true)),
                    Row(I("excel.home.align.left", "왼쪽 맞춤", "aleft", toggle: true), I("excel.home.align.center", "가운데 맞춤", "acenter", toggle: true), I("excel.home.align.right", "오른쪽 맞춤", "aright", toggle: true), I("excel.home.merge", "병합하고 가운데 맞춤", "mergec", toggle: true))))),
            Pick(level,
                (1, Grid("표시 형식",
                    L("excel.home.number.menu", "표시 형식", glyph: "%", dd: true))),
                (3, Rows("표시 형식",
                    Row(D("excel.home.numberformat", "표시 형식", "일반", 136)),
                    Row(I("excel.home.accounting", "회계 표시 형식", glyph: "₩", dd: true, w: 56), I("excel.home.percent", "백분율 스타일", glyph: "%", key: "Ctrl+Shift+%", toggle: true), I("excel.home.comma", "쉼표 스타일", glyph: ",", toggle: true))))),
            Pick(level,
                (1, Grid("스타일",
                    L("excel.home.styles.menu", "스타일", "cstyle", dd: true))),
                (3, Grid("스타일",
                    L("excel.home.condformat", "조건부 서식", "cond", dd: true),
                    L("excel.home.formattable", "표 서식", "table", dd: true),
                    L("excel.home.cellstyles", "셀 스타일", "cstyle", dd: true)))),
            Pick(level,
                (1, Grid("셀",
                    L("excel.home.cells.menu", "셀", "cins", dd: true))),
                (3, Grid("셀",
                    L("excel.home.insertcells", "삽입", "cins", dd: true),
                    L("excel.home.deletecells", "삭제", "cdel", dd: true),
                    L("excel.home.formatcells", "서식", "cfmt", dd: true)))),
            Grid("편집",
                L("excel.home.autosum", "자동 합계", "sigma", dd: true, key: "Alt+="),
                I("excel.home.sortfilter", "정렬 및 필터", "sortf", bar: ""),
                I("excel.home.findselect", "찾기 및 선택", "search", key: "Ctrl+F", bar: ""))));

        private static RibbonTab InsertTab(int level) => Tab("insert", "삽입", Groups(
            Grid("표",
                L("excel.insert.pivot", "피벗 테이블", "pivot", dd: true),
                L("excel.insert.table", "표", "table", key: "Ctrl+T", toggle: true)),
            Pick(level,
                (1, Grid("일러스트레이션",
                    L("excel.insert.picture", "그림", "image", dd: true),
                    I("excel.insert.shapes", "도형", "shapes", bar: ""),
                    I("excel.insert.icons", "아이콘", "smile"))),
                (2, Grid("일러스트레이션",
                    L("excel.insert.picture", "그림", "image", dd: true),
                    S("excel.insert.shapes", "도형", "shapes", dd: true),
                    S("excel.insert.icons", "아이콘", "smile")))),
            Grid("차트",
                L("excel.insert.chart.recommended", "추천 차트", "chart"),
                I("excel.insert.chart.column", "세로 막대형 차트", "chart", dd: true, w: 56),
                I("excel.insert.chart.line", "꺾은선형 차트", "lchart", dd: true, w: 56),
                I("excel.insert.chart.pie", "원형 차트", "pie", dd: true, w: 56)),
            Grid("링크",
                L("excel.insert.link", "링크", "link", key: "Ctrl+K", toggle: true)),
            Pick(level,
                (2, Grid("메모",
                    L("excel.insert.note", "메모", "comment")))),
            Grid("텍스트",
                L("excel.insert.textbox", "텍스트 상자", "textbox"),
                L("excel.insert.headerfooter", "머리글/바닥글", "header")),
            Pick(level,
                (1, Grid("기호",
                    I("excel.insert.equation", "수식", "pi", bar: ""),
                    I("excel.insert.symbol", "기호", "omega"))),
                (2, Grid("기호",
                    S("excel.insert.equation", "수식", "pi", dd: true),
                    S("excel.insert.symbol", "기호", "omega"))))));

        private static RibbonTab LayoutTab(int level) => Tab("layout", "페이지 레이아웃", Groups(
            Pick(level,
                (1, Grid("테마",
                    L("excel.layout.theme", "테마", "theme", dd: true))),
                (2, Grid("테마",
                    L("excel.layout.theme", "테마", "theme", dd: true),
                    I("excel.layout.themecolors", "색", "colors", dd: true, w: 56),
                    I("excel.layout.themefonts", "글꼴", "type", dd: true, w: 56)))),
            Pick(level,
                (1, Grid("페이지 설정",
                    L("excel.layout.margins", "여백", "margins", dd: true, toggle: true),
                    L("excel.layout.orientation", "용지 방향", "orient", dd: true, toggle: true),
                    L("excel.layout.papersize", "크기", "size", dd: true, toggle: true),
                    L("excel.layout.printarea", "인쇄 영역", "parea", dd: true, toggle: true),
                    L("excel.layout.breaks", "나누기", "pbreak", dd: true))),
                (2, Grid("페이지 설정",
                    L("excel.layout.margins", "여백", "margins", dd: true, toggle: true),
                    L("excel.layout.orientation", "용지 방향", "orient", dd: true, toggle: true),
                    L("excel.layout.papersize", "크기", "size", dd: true, toggle: true),
                    L("excel.layout.printarea", "인쇄 영역", "parea", dd: true, toggle: true),
                    L("excel.layout.breaks", "나누기", "pbreak", dd: true),
                    L("excel.layout.background", "배경", "image", toggle: true),
                    L("excel.layout.printtitles", "인쇄 제목", "ptitle")))),
            Pick(level,
                (1, Grid("크기 조정",
                    D("excel.layout.fitwidth", "너비", "자동", 124, prefix: "너비: "),
                    D("excel.layout.fitheight", "높이", "자동", 124, prefix: "높이: "))),
                (3, Grid("크기 조정",
                    D("excel.layout.fitwidth", "너비", "자동", 124, prefix: "너비: "),
                    D("excel.layout.fitheight", "높이", "자동", 124, prefix: "높이: "),
                    D("excel.layout.scale", "배율", "100%", 124, prefix: "배율: ")))),
            Grid("시트 옵션",
                C("excel.layout.gridlines.view", "눈금선 보기"),
                C("excel.layout.gridlines.print", "눈금선 인쇄"),
                C("excel.layout.headings.view", "제목 보기"),
                C("excel.layout.headings.print", "제목 인쇄"))));

        private static RibbonTab FormulaTab(int level) => Tab("formula", "수식", Groups(
            Pick(level,
                (1, Grid("함수 라이브러리",
                    L("excel.formula.insertfn", "함수 삽입", glyph: "fx", key: "Shift+F3"),
                    L("excel.formula.autosum", "자동 합계", "sigma", dd: true, key: "Alt+="),
                    S("excel.formula.logical", "논리", "logic", dd: true),
                    S("excel.formula.text", "텍스트", "type", dd: true),
                    S("excel.formula.datetime", "날짜 및 시간", "calendar", dd: true),
                    S("excel.formula.lookup", "찾기/참조 영역", "search", dd: true))),
                (2, Grid("함수 라이브러리",
                    L("excel.formula.insertfn", "함수 삽입", glyph: "fx", key: "Shift+F3"),
                    L("excel.formula.autosum", "자동 합계", "sigma", dd: true, key: "Alt+="),
                    S("excel.formula.logical", "논리", "logic", dd: true),
                    S("excel.formula.text", "텍스트", "type", dd: true),
                    S("excel.formula.datetime", "날짜 및 시간", "calendar", dd: true),
                    S("excel.formula.lookup", "찾기/참조 영역", "search", dd: true),
                    S("excel.formula.math", "수학/삼각", "pi", dd: true),
                    S("excel.formula.more", "함수 더 보기", "more", dd: true)))),
            Grid("정의된 이름",
                L("excel.formula.namemanager", "이름 관리자", "tag", key: "Ctrl+F3")),
            Grid("수식 분석",
                S("excel.formula.showformulas", "수식 표시", "showf", key: "Ctrl+`", toggle: true),
                S("excel.formula.errorcheck", "오류 검사", "warn", dd: true)),
            Grid("계산",
                L("excel.formula.calcoptions", "계산 옵션", "calc", dd: true, toggle: true))));

        private static RibbonTab DataTab(int level) => Tab("data", "데이터", Groups(
            Pick(level,
                (1, Grid("데이터 가져오기 및 변환",
                    L("excel.data.getdata", "데이터 가져오기", "db", dd: true))),
                (3, Grid("데이터 가져오기 및 변환",
                    L("excel.data.getdata", "데이터 가져오기", "db", dd: true),
                    S("excel.data.fromtext", "텍스트/CSV에서", "page")))),
            Pick(level,
                (2, Grid("쿼리 및 연결",
                    L("excel.data.refreshall", "모두 새로 고침", "refresh", dd: true, key: "Ctrl+Alt+F5")))),
            Grid("정렬 및 필터",
                I("excel.data.sort.asc", "텍스트 오름차순 정렬", "az"),
                I("excel.data.sort.desc", "텍스트 내림차순 정렬", "za"),
                L("excel.data.sort", "정렬", "sort"),
                L("excel.data.filter", "필터", "filter", key: "Ctrl+Shift+L", toggle: true),
                S("excel.data.filter.clear", "지우기", "fclear"),
                S("excel.data.filter.reapply", "다시 적용", "refresh")),
            Pick(level,
                (1, Grid("데이터 도구",
                    L("excel.data.texttocolumns", "텍스트 나누기", "cols3"),
                    I("excel.data.flashfill", "빠른 채우기", "zap", key: "Ctrl+E"),
                    I("excel.data.removeduplicates", "중복된 항목 제거", "dedupe"),
                    I("excel.data.validation", "데이터 유효성 검사", "valid", dd: true, w: 56))),
                (3, Grid("데이터 도구",
                    L("excel.data.texttocolumns", "텍스트 나누기", "cols3"),
                    L("excel.data.flashfill", "빠른 채우기", "zap", key: "Ctrl+E"),
                    L("excel.data.removeduplicates", "중복된 항목 제거", "dedupe"),
                    L("excel.data.validation", "데이터 유효성 검사", "valid", dd: true)))),
            Grid("예측",
                L("excel.data.whatif", "가상 분석", "whatif", dd: true)),
            Pick(level,
                (1, Grid("개요",
                    L("excel.data.outline.menu", "개요", "group", dd: true))),
                (2, Grid("개요",
                    L("excel.data.group", "그룹", "group", dd: true),
                    L("excel.data.ungroup", "그룹 해제", "ungroup", dd: true),
                    L("excel.data.subtotal", "부분합", "subtot"))))));

        private static RibbonTab ReviewTab(int level) => Tab("review", "검토", Groups(
            Grid("언어 교정",
                L("excel.review.spelling", "맞춤법 검사", "spell", key: "F7"),
                L("excel.review.thesaurus", "동의어 사전", "thes", key: "Shift+F7")),
            Grid("접근성",
                L("excel.review.accessibility", "접근성 검사", "access", dd: true)),
            Grid("언어",
                L("excel.review.translate", "번역", "lang")),
            Pick(level,
                (1, Grid("메모",
                    L("excel.review.newnote", "새 메모", "comment", key: "Shift+F2"),
                    L("excel.review.shownote", "메모 표시", "cmtshow", toggle: true))),
                (2, Grid("메모",
                    L("excel.review.newnote", "새 메모", "comment", key: "Shift+F2"),
                    L("excel.review.shownote", "메모 표시", "cmtshow", toggle: true),
                    S("excel.review.deletenote", "삭제", "trash"),
                    S("excel.review.prevnote", "이전 메모", "up"),
                    S("excel.review.nextnote", "다음 메모", "down")))),
            Pick(level,
                (1, Grid("보호",
                    L("excel.review.protectsheet", "시트 보호", "lock", toggle: true),
                    L("excel.review.protectbook", "통합 문서 보호", "booklock", toggle: true))),
                (2, Grid("보호",
                    L("excel.review.protectsheet", "시트 보호", "lock", toggle: true),
                    L("excel.review.protectbook", "통합 문서 보호", "booklock", toggle: true),
                    S("excel.review.allowranges", "범위 편집 허용", "listedit"))))));

        private static RibbonTab ViewTab(int level) => Tab("view", "보기", Groups(
            Grid("통합 문서 보기",
                L("excel.view.normal", "기본", "normal", toggle: true),
                L("excel.view.pagebreak", "페이지 나누기 미리 보기", "pbview", toggle: true),
                L("excel.view.pagelayout", "페이지 레이아웃", "plview", toggle: true)),
            Grid("표시",
                C("excel.view.ruler", "눈금자"),
                C("excel.view.formulabar", "수식 입력줄"),
                C("excel.view.gridlines", "눈금선"),
                C("excel.view.headings", "머리글")),
            Grid("확대/축소",
                L("excel.view.zoom", "확대/축소", "zoom"),
                L("excel.view.zoom100", "100%", "z100"),
                L("excel.view.zoomselection", "선택 영역 확대/축소", "zsel")),
            Pick(level,
                (1, Grid("창",
                    L("excel.view.freeze", "틀 고정", "freeze", dd: true, toggle: true))),
                (2, Grid("창",
                    L("excel.view.freeze", "틀 고정", "freeze", dd: true, toggle: true),
                    S("excel.view.newwindow", "새 창", "newwin"),
                    S("excel.view.arrangeall", "모두 정렬", "side"),
                    S("excel.view.split", "나누기", "border"),
                    S("excel.view.hidewindow", "숨기기", "eyeoff"))))));

    }
}
