using System.Collections.Generic;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpFeatureNames
    {
        private static readonly Dictionary<HwpFeature, (string Korean, string English)> s_names = new()
        {
            [HwpFeature.CharShade] = ("형광펜·글자 음영", "highlight and text shading"),
            [HwpFeature.CharSpacing] = ("자간·장평", "character spacing and width scaling"),
            [HwpFeature.ParaRightIndent] = ("오른쪽 여백", "right indent"),
            [HwpFeature.ParaKeep] = ("다음 문단과 함께", "keep with next paragraph"),
            [HwpFeature.ParaFixedLine] = ("고정·최소 줄 간격", "exact and at-least line spacing"),
            [HwpFeature.ParaDivide] = ("나눔 정렬", "distributed (나눔) alignment"),
            [HwpFeature.ParaBorder] = ("문단 테두리·음영", "paragraph borders and shading"),
            [HwpFeature.TabStops] = ("탭 정지", "tab stops and leaders"),
            [HwpFeature.TableCellFormat] = ("표 칸 모양", "table cell shading, borders and row heights"),
            [HwpFeature.HeaderFooter] = ("머리말·꼬리말", "headers and footers"),
            [HwpFeature.PageNumber] = ("쪽 번호", "page numbers"),
            [HwpFeature.PageControl] = ("쪽 감추기·새 번호", "page hiding and restarted page numbers"),
            [HwpFeature.Columns] = ("단", "columns"),
            [HwpFeature.PageBorder] = ("쪽 테두리·배경", "page borders and background"),
            [HwpFeature.Sections] = ("구역", "sections"),
            [HwpFeature.Pictures] = ("그림", "pictures"),
            [HwpFeature.Notes] = ("각주·미주", "footnotes and endnotes"),
            [HwpFeature.Hyperlinks] = ("하이퍼링크", "hyperlinks"),
            [HwpFeature.Bookmarks] = ("책갈피", "bookmarks"),
            [HwpFeature.ClickHere] = ("누름틀", "click-here fields"),
            [HwpFeature.NamedStyles] = ("스타일 이름", "style names"),
            [HwpFeature.RealNumbering] = ("자동 번호 매기기", "automatic list numbering"),
            [HwpFeature.KeepObjects] = ("수식·차트 같은 개체", "equations, charts and other objects"),
        };

        private static readonly HwpFeature[] s_prompt =
        {
            HwpFeature.Pictures,
            HwpFeature.KeepObjects,
            HwpFeature.HeaderFooter,
            HwpFeature.Notes,
            HwpFeature.Hyperlinks,
            HwpFeature.Bookmarks,
            HwpFeature.ClickHere,
        };

        public static IReadOnlyList<HwpFeature> Prompt => s_prompt;

        public static string Korean(HwpFeature feature) => s_names[feature].Korean;

        public static string English(HwpFeature feature) => s_names[feature].English;
    }
}
