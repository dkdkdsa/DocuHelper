namespace DocuHelperEditor.Components.Paper.Dash
{
    public static class DashToolbarItems
    {
        public const string ViewId = "dash.view.show";
        public const string EditId = "dash.view.edit";
        public const string DataId = "dash.view.data";
        public const string FitId = "dash.view.fit";
        public const string ControlId = "dash.insert.control";

        public static DashToolbarItem[] Tools { get; } =
        {
            new("글자", "글자 상자 넣기", "M4 7V4h16v3 M9 20h6 M12 4v16", "dash.insert.textbox"),
            new("값", "엑셀 값 하나 넣기", "M4 9h16 M4 15h16 M10 3 8 21 M16 3l-2 18", "dash.insert.textbox", null, true),
            new("도형", "네모 넣기", "M5 4h14a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1Z", "dash.insert.shapes", "m:0"),
            new("선", "선 넣기", "M4 12h16", "dash.insert.shapes", "m:4"),
            new("그래프", "그래프 넣기", "M3 3v18h18 M7 16v-5 M12 16V8 M17 16v-9", "dash.insert.chart", "m:0"),
            new("표", "표 넣기", "M3 5h18v14H3Z M3 10h18 M3 15h18 M9 5v14", "dash.insert.table"),
            new("반복", "표의 줄마다 같은 모양 만들기", "M8 6h13 M8 12h13 M8 18h13 M3 6h.01 M3 12h.01 M3 18h.01", "dash.insert.repeat"),
        };

        public static DashToolbarItem[] Controls { get; } =
        {
            new("단추", "누르면 조절값을 바꾸거나 도우미에게 부탁", "M4 7h16v10H4Z M9 12h6", ControlId, "m:0"),
            new("슬라이더", "숫자 조절값 하나", "M3 12h18 M9 8v8", ControlId, "m:1"),
            new("기간 슬라이더", "손잡이 둘로 시작과 끝", "M3 12h18 M7 8v8 M17 8v8", ControlId, "m:2"),
            new("스위치", "켜기, 끄기", "M7 7h10a5 5 0 0 1 0 10H7A5 5 0 0 1 7 7Z M16 12h.01", ControlId, "m:3"),
            new("단추 줄", "몇 개 중 하나 고르기", "M3 7h18v10H3Z M9 7v10 M15 7v10", ControlId, "m:4"),
            new("펼침 목록", "많은 것 중 하나 고르기", "M4 7h16v10H4Z M13 11l2 2 2-2", ControlId, "m:5"),
            new("진행 막대", "값만큼 차는 막대", "M3 10h18v4H3Z M3 12h11", "dash.insert.meter"),
        };
    }
}
