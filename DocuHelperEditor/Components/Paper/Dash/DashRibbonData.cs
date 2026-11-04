using System.Collections.Generic;
using DocuHelperEditor.State.Ribbon;
using static DocuHelperEditor.State.Ribbon.RibbonBuilder;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashRibbonData
    {
        public static IReadOnlyList<RibbonTab> Tabs { get; } = new[]
        {
            Tab("home", "홈",
                Grid("클립보드",
                    L("dash.home.paste", "붙여넣기", "paste", key: "Ctrl+V"),
                    I("dash.home.cut", "잘라내기", "cut", key: "Ctrl+X", aria: "잘라내기 (Ctrl+X)"),
                    I("dash.home.copy", "복사", "copy", key: "Ctrl+C", aria: "복사 (Ctrl+C)")),
                Rows("글꼴",
                    Row(
                        D("dash.home.fontName", "글꼴", "Gothic A1", 112),
                        D("dash.home.fontSize", "글꼴 크기", "16", 60),
                        I("dash.home.fontGrow", "글꼴 크기 크게", "grow"),
                        I("dash.home.fontShrink", "글꼴 크기 작게", "shrink")),
                    Row(
                        I("dash.home.bold", "굵게", "bold", key: "Ctrl+B", aria: "굵게 (Ctrl+B)", toggle: true),
                        I("dash.home.italic", "기울임꼴", "italic", key: "Ctrl+I", aria: "기울임꼴 (Ctrl+I)", toggle: true),
                        I("dash.home.underline", "밑줄", "under", key: "Ctrl+U", aria: "밑줄 (Ctrl+U)", toggle: true),
                        I("dash.home.fontColor", "글꼴 색", "fcolor", bar: "1C2A48"))),
                Rows("단락",
                    Row(
                        I("dash.home.alignLeft", "텍스트 왼쪽 맞춤", "aleft", toggle: true),
                        I("dash.home.alignCenter", "가운데 맞춤", "acenter", toggle: true),
                        I("dash.home.alignRight", "텍스트 오른쪽 맞춤", "aright", toggle: true)),
                    Row(
                        I("dash.home.mark", "형광펜", "hilite", toggle: true, bar: "FFE35A"))),
                Grid("그리기",
                    L("dash.home.shapes", "도형", "shapes", dd: true),
                    L("dash.home.arrange", "정렬", "front", dd: true)),
                Grid("요소",
                    L("dash.home.settings", "설정", "sliders", tip: "설정\n고른 요소의 연결, 언제 보일지, 자리와 크기를 바꿔요"),
                    L("dash.home.delete", "지우기", "del"))),
            Tab("insert", "삽입",
                Grid("글자",
                    L("dash.insert.textbox", "텍스트 상자", "textbox")),
                Grid("일러스트레이션",
                    L("dash.insert.shapes", "도형", "shapes", dd: true)),
                Grid("데이터 보기",
                    L("dash.insert.chart", "차트", "chart", dd: true),
                    L("dash.insert.table", "표", "table"),
                    L("dash.insert.repeat", "반복 목록", "list")),
                Grid("조절 장치",
                    L("dash.insert.control", "조절 장치", "sliders", dd: true, tip: "조절 장치\n보는 사람이 바꿔 보는 단추, 슬라이더, 스위치를 넣어요"),
                    L("dash.insert.meter", "진행 막대", "meter")),
                Grid("묶음",
                    S("dash.insert.row", "가로로 늘어놓기", "cols"),
                    S("dash.insert.column", "세로로 쌓기", "stack"),
                    S("dash.insert.scroll", "스크롤 칸", "stack", tip: "스크롤 칸\n넘치는 내용을 칸 안에서 위아래로 넘겨 봐요"),
                    S("dash.insert.canvas", "자유 배치 칸", "normal"))),
            Tab("data", "데이터",
                Grid("원본",
                    L("dash.data.source", "원본 연결", "link", dd: true, tip: "원본 연결\n엑셀 시트, 워드·한글 표, PPT 표나 차트를 읽어 와요"),
                    L("dash.data.refresh", "다시 읽기", "refresh", tip: "다시 읽기\n원본 파일을 다시 읽어서 숫자를 새로 셈해요")),
                Grid("조절값",
                    L("dash.data.param", "조절값 추가", "sliders", dd: true),
                    L("dash.data.resetParams", "처음 값으로", "undo", tip: "처음 값으로\n바꿔 본 조절값을 파일에 적힌 처음 값으로 돌려요")),
                Grid("계산",
                    L("dash.data.table", "새 표", "table"),
                    L("dash.data.formula", "새 값", "sigma", tip: "새 값\n엑셀 수식으로 값을 셈해요"))),
            Tab("view", "보기",
                Grid("대시보드 보기",
                    L("dash.view.show", "보기", "read", toggle: true, tip: "보기\n보는 사람처럼 단추와 슬라이더를 눌러 봐요"),
                    L("dash.view.edit", "고치기", "normal", toggle: true, tip: "고치기\n글자, 그래프, 조절 장치를 놓고 옮겨요"),
                    L("dash.view.data", "데이터", "table", toggle: true, tip: "데이터\n원본 표를 보면서 계산 순서를 만들어요")),
                Grid("표시",
                    C("dash.view.gridlines", "눈금선"),
                    C("dash.view.guides", "안내선")),
                Grid("확대/축소",
                    L("dash.view.zoom", "확대/축소", "zoom"),
                    L("dash.view.fit", "창에 맞춤", "fit"))),
        };

        public static IReadOnlyList<RibbonFileItem> FileItems { get; } = new[]
        {
            FileItem("dash.file.save", "저장", "Ctrl+S"),
            FileItem("dash.file.saveAs", "다른 이름으로 저장"),
            FileItem("dash.file.pdf", "PDF로 저장"),
            FileItem("dash.file.print", "인쇄", "Ctrl+P"),
            FileItem("dash.file.info", "문서 정보"),
        };
    }
}
