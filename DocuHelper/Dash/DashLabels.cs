using System.Collections.Generic;
using DocuHelper.Dash.Elements;

namespace DocuHelper.Dash
{
    public static class DashLabels
    {
        private static readonly Dictionary<string, string> s_elements = new()
        {
            ["page"] = "페이지",
            ["canvas"] = "자유 배치 칸",
            ["row"] = "가로 묶음",
            ["column"] = "세로 묶음",
            ["scroll"] = "스크롤 칸",
            ["repeat"] = "반복 목록",
            ["text"] = "글자",
            ["shape"] = "도형",
            ["line"] = "선",
            ["chart"] = "차트",
            ["table"] = "표",
            ["meter"] = "진행 막대",
            ["slider"] = "슬라이더",
            ["rangeSlider"] = "기간 슬라이더",
            ["toggle"] = "스위치",
            ["segmented"] = "단추 줄",
            ["select"] = "펼침 목록",
            ["button"] = "단추",
        };

        public static string Kind(string kind) => kind != null && s_elements.TryGetValue(kind, out var label) ? label : "요소";

        public static string Of(Element element)
        {
            if (element == null)
                return "요소";

            if (!string.IsNullOrWhiteSpace(element.Name))
                return element.Name.Trim();

            if (element is TextElement text && !string.IsNullOrWhiteSpace(text.Text))
                return Shorten(text.Text);

            if (element is ButtonElement button && !string.IsNullOrWhiteSpace(button.Label))
                return button.Label.Trim() + " 단추";

            return Kind(element.Kind);
        }

        private static string Shorten(string text)
        {
            var value = text.Replace('\n', ' ').Trim();
            return value.Length <= 16 ? value : value.Substring(0, 16).TrimEnd() + "…";
        }
    }
}
