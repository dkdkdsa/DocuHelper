using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Dash
{
    public static class DashTemplates
    {
        public const string Summary = "summary";

        private const string Header = """
                { "type": "text", "id": "title", "name": "제목", "style": { "x": 56, "y": 44, "w": 700, "h": 52, "fontName": "Hahmlet", "fontSize": 40, "fontWeight": 600 }, "text": "대시보드 제목" },
                { "type": "text", "id": "subtitle", "name": "기준 글", "style": { "x": 56, "y": 100, "w": 700, "h": 22, "fontSize": 13, "color": "#4A5570" }, "text": "무엇을 언제 기준으로 보는지 적어요" },
                { "type": "shape", "id": "rule", "name": "위 가로줄", "shape": "rect", "style": { "x": 56, "y": 136, "w": 1011, "h": 2, "fill": "#1C2A48" } },
            """;

        private const string SummaryBody = """
                { "type": "shape", "id": "divider", "name": "가운데 세로줄", "shape": "rect", "style": { "x": 559, "y": 160, "w": 2, "h": 586, "fill": "#E3E7EE" } },
                { "type": "text", "id": "hero", "name": "큰 숫자", "style": { "x": 56, "y": 160, "w": 470, "h": 84, "fontSize": 72, "fontWeight": 700, "mark": "#FFE35A" }, "text": "큰 숫자" },
                { "type": "text", "id": "heroNote", "name": "큰 숫자 설명", "style": { "x": 56, "y": 250, "w": 470, "h": 30, "fontSize": 18, "fontWeight": 500 }, "text": "큰 숫자를 풀어 쓰는 한 줄" },
                { "type": "text", "id": "kpi1Label", "name": "숫자 1 이름", "style": { "x": 56, "y": 306, "w": 130, "h": 20, "fontSize": 13, "color": "#4A5570" }, "text": "숫자 1" },
                { "type": "text", "id": "kpi1", "name": "숫자 1", "style": { "x": 56, "y": 328, "w": 130, "h": 32, "fontSize": 22, "fontWeight": 700 }, "text": "0" },
                { "type": "text", "id": "kpi2Label", "name": "숫자 2 이름", "style": { "x": 196, "y": 306, "w": 160, "h": 20, "fontSize": 13, "color": "#4A5570" }, "text": "숫자 2" },
                { "type": "text", "id": "kpi2", "name": "숫자 2", "style": { "x": 196, "y": 328, "w": 160, "h": 32, "fontSize": 22, "fontWeight": 700 }, "text": "0" },
                { "type": "text", "id": "kpi3Label", "name": "숫자 3 이름", "style": { "x": 370, "y": 306, "w": 156, "h": 20, "fontSize": 13, "color": "#4A5570" }, "text": "숫자 3" },
                { "type": "text", "id": "kpi3", "name": "숫자 3", "style": { "x": 370, "y": 328, "w": 156, "h": 32, "fontSize": 22, "fontWeight": 700 }, "text": "0" },
                { "type": "text", "id": "trendTitle", "name": "흐름 제목", "style": { "x": 56, "y": 400, "w": 300, "h": 30, "fontName": "Hahmlet", "fontSize": 22, "fontWeight": 600 }, "text": "흐름" },
                { "type": "chart", "id": "trend", "name": "흐름 그래프", "kind": "column", "emphasis": "max", "style": { "x": 56, "y": 444, "w": 470, "h": 250, "fontSize": 11 } },
                { "type": "text", "id": "rankTitle", "name": "순위 제목", "style": { "x": 600, "y": 160, "w": 300, "h": 30, "fontName": "Hahmlet", "fontSize": 22, "fontWeight": 600 }, "text": "순위" },
                { "type": "repeat", "id": "rank", "name": "순위 목록", "max": 6, "empty": "보여 줄 줄이 없어요", "style": { "x": 600, "y": 204, "w": 467, "h": 390, "gap": 6, "fontSize": 14 },
                  "template": [
                    { "type": "text", "id": "rankName", "name": "이름", "style": { "w": 150, "h": 28, "fontSize": 16, "fontWeight": 700 }, "text": "«item.이름»" },
                    { "type": "meter", "id": "rankBar", "name": "막대", "style": { "grow": 1, "h": 28 }, "value": "item.비율" },
                    { "type": "text", "id": "rankAmount", "name": "금액", "style": { "w": 100, "h": 28, "fontSize": 16, "fontWeight": 700, "textAlign": "right" }, "text": "«item.금액»" }
                  ] },
                { "type": "text", "id": "memo", "name": "순위 설명", "style": { "x": 600, "y": 616, "w": 467, "h": 52, "fontSize": 15 }, "text": "순위를 풀어 쓰는 한 줄" }
            """;

        private const string ChartBody = """
                { "type": "text", "id": "kpi1Label", "name": "숫자 1 이름", "style": { "x": 56, "y": 168, "w": 230, "h": 20, "fontSize": 13, "color": "#4A5570" }, "text": "숫자 1" },
                { "type": "text", "id": "kpi1", "name": "숫자 1", "style": { "x": 56, "y": 190, "w": 230, "h": 46, "fontSize": 34, "fontWeight": 700 }, "text": "0" },
                { "type": "text", "id": "kpi2Label", "name": "숫자 2 이름", "style": { "x": 322, "y": 168, "w": 120, "h": 20, "fontSize": 13, "color": "#4A5570" }, "text": "숫자 2" },
                { "type": "text", "id": "kpi2", "name": "숫자 2", "style": { "x": 322, "y": 194, "w": 120, "h": 38, "fontSize": 26, "fontWeight": 700 }, "text": "0" },
                { "type": "text", "id": "kpi3Label", "name": "숫자 3 이름", "style": { "x": 478, "y": 168, "w": 170, "h": 20, "fontSize": 13, "color": "#4A5570" }, "text": "숫자 3" },
                { "type": "text", "id": "kpi3", "name": "숫자 3", "style": { "x": 478, "y": 194, "w": 170, "h": 38, "fontSize": 26, "fontWeight": 700 }, "text": "0" },
                { "type": "text", "id": "kpi4Label", "name": "숫자 4 이름", "style": { "x": 684, "y": 168, "w": 180, "h": 20, "fontSize": 13, "color": "#4A5570" }, "text": "숫자 4" },
                { "type": "text", "id": "kpi4", "name": "숫자 4", "style": { "x": 684, "y": 194, "w": 180, "h": 38, "fontSize": 26, "fontWeight": 700 }, "text": "0" },
                { "type": "text", "id": "trendTitle", "name": "흐름 제목", "style": { "x": 56, "y": 260, "w": 400, "h": 28, "fontName": "Hahmlet", "fontSize": 20, "fontWeight": 600 }, "text": "흐름" },
                { "type": "chart", "id": "trend", "name": "흐름 그래프", "kind": "column", "emphasis": "max", "style": { "x": 56, "y": 294, "w": 1011, "h": 200, "fontSize": 11 } },
                { "type": "text", "id": "leftTitle", "name": "왼쪽 그래프 제목", "style": { "x": 56, "y": 512, "w": 480, "h": 26, "fontName": "Hahmlet", "fontSize": 18, "fontWeight": 600 }, "text": "나눠 보기 1" },
                { "type": "chart", "id": "left", "name": "왼쪽 그래프", "kind": "bar", "emphasis": "max", "labels": true, "style": { "x": 56, "y": 546, "w": 480, "h": 216, "fontSize": 12 } },
                { "type": "text", "id": "rightTitle", "name": "오른쪽 그래프 제목", "style": { "x": 587, "y": 512, "w": 480, "h": 26, "fontName": "Hahmlet", "fontSize": 18, "fontWeight": 600 }, "text": "나눠 보기 2" },
                { "type": "chart", "id": "right", "name": "오른쪽 그래프", "kind": "bar", "emphasis": "max", "labels": true, "style": { "x": 587, "y": 546, "w": 480, "h": 216, "fontSize": 12 } }
            """;

        private const string TableBody = """
                { "type": "text", "id": "tableTitle", "name": "표 제목", "style": { "x": 56, "y": 168, "w": 600, "h": 30, "fontName": "Hahmlet", "fontSize": 20, "fontWeight": 600 }, "text": "표 제목" },
                { "type": "table", "id": "table", "name": "표", "max": 10, "style": { "x": 56, "y": 208, "w": 1011, "h": 452, "fontSize": 14 } },
                { "type": "text", "id": "tableNote", "name": "표 설명", "style": { "x": 56, "y": 680, "w": 1011, "h": 26, "fontSize": 13, "color": "#4A5570" }, "text": "표 아래에 덧붙이는 말" }
            """;

        private const string CardsListBody = """
                { "type": "text", "id": "listTitle", "name": "카드 목록 제목", "style": { "x": 56, "y": 450, "w": 600, "h": 30, "fontName": "Hahmlet", "fontSize": 20, "fontWeight": 600 }, "text": "하나씩 보기" },
                { "type": "repeat", "id": "cards", "name": "카드 목록", "direction": "row", "max": 6, "empty": "보여 줄 줄이 없어요", "style": { "x": 56, "y": 488, "w": 1011, "h": 160, "gap": 12 },
                  "template": [
                    { "type": "column", "id": "card", "name": "카드", "style": { "w": 158, "h": 150, "padding": 14, "gap": 6, "fill": "#FFFFFF", "line": "#CBD1DB", "lineWidth": 1.5, "radius": 12 },
                      "children": [
                        { "type": "text", "id": "cardName", "name": "이름", "style": { "h": 24, "fontSize": 15, "fontWeight": 700 }, "text": "«item.이름»" },
                        { "type": "text", "id": "cardAmount", "name": "금액", "style": { "h": 30, "fontSize": 20, "fontWeight": 700 }, "text": "«item.금액»" },
                        { "type": "meter", "id": "cardBar", "name": "막대", "style": { "h": 8 }, "value": "item.비율" },
                        { "type": "text", "id": "cardNote", "name": "덧붙임", "style": { "h": 20, "fontSize": 13, "color": "#4A5570" }, "text": "«item.비율»" }
                      ] }
                  ] }
            """;

        private static readonly IReadOnlyList<DashTemplate> s_all = new[]
        {
            new DashTemplate { Id = Summary, Title = "한 장 요약", Description = "큰 숫자, 흐름 그래프, 순위를 한 장에", Json = Page(SummaryBody) },
            new DashTemplate { Id = "chart", Title = "그래프 위주", Description = "흐름과 나눠 보기를 큰 그래프로", Json = Page(ChartBody) },
            new DashTemplate { Id = "cards", Title = "카드로 보기", Description = "중요한 숫자는 카드로, 항목마다 카드 한 장", Json = Page(CardsBody()) },
            new DashTemplate { Id = "table", Title = "표로 보기", Description = "엑셀처럼 표 하나에 합계까지", Json = Page(TableBody) },
        };

        public static IReadOnlyList<DashTemplate> All => s_all;

        public static DashTemplate Find(string id) =>
            s_all.FirstOrDefault(template => string.Equals(template.Id, id, StringComparison.OrdinalIgnoreCase));

        private static string CardsBody()
        {
            var cards = new List<string>();
            for (var i = 0; i < 6; i++)
            {
                var x = 56 + i % 3 * 344;
                var y = 168 + i / 3 * 136;
                var n = i + 1;
                cards.Add($$"""{ "type": "shape", "id": "card{{n}}", "name": "숫자 카드 {{n}}", "shape": "round", "style": { "x": {{x}}, "y": {{y}}, "w": 324, "h": 120, "fill": "#FFFFFF", "line": "#CBD1DB", "lineWidth": 1.5, "radius": 14 } }""");
                cards.Add($$"""{ "type": "text", "id": "kpi{{n}}Label", "name": "숫자 {{n}} 이름", "style": { "x": {{x + 18}}, "y": {{y + 12}}, "w": 288, "h": 20, "fontSize": 14, "color": "#4A5570" }, "text": "숫자 {{n}}" }""");
                cards.Add($$"""{ "type": "text", "id": "kpi{{n}}", "name": "숫자 {{n}}", "style": { "x": {{x + 18}}, "y": {{y + 34}}, "w": 288, "h": 40, "fontSize": 30, "fontWeight": 700 }, "text": "0" }""");
                cards.Add($$"""{ "type": "text", "id": "kpi{{n}}Note", "name": "숫자 {{n}} 설명", "style": { "x": {{x + 18}}, "y": {{y + 80}}, "w": 288, "h": 20, "fontSize": 13, "color": "#4A5570" }, "text": "한 줄 설명" }""");
            }

            return string.Join(",\n", cards) + ",\n" + CardsListBody;
        }

        private static string Page(string body) =>
            "{ \"kind\": \"dash\", \"version\": 1, \"root\": { \"id\": \"page\", \"paper\": \"a4l\", \"width\": 1123, \"height\": 794, \"style\": { \"fill\": \"#FFFFFF\" }, \"children\": [\n"
            + Header + "\n" + body + "\n] } }";
    }
}
