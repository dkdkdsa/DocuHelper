using System;
using System.Collections.Generic;

namespace DocuHelper.Tools.Word
{
    public static class WordPresets
    {
        public static IReadOnlyDictionary<string, WordMarginPreset> Margins { get; } = new Dictionary<string, WordMarginPreset>(StringComparer.OrdinalIgnoreCase)
        {
            ["normal"] = new("normal", "보통", 2.54, 2.54, 2.54, 2.54, null, null, null, null, false),
            ["narrow"] = new("narrow", "좁게", 1.27, 1.27, 1.27, 1.27, null, null, null, null, false),
            ["moderate"] = new("moderate", "중간", 2.54, 2.54, 1.91, 1.91, null, null, null, null, false),
            ["wide"] = new("wide", "넓게", 2.54, 2.54, 5.08, 5.08, null, null, null, null, false),
            ["mirrored"] = new("mirrored", "마주 보기", 2.54, 2.54, null, null, 3.18, 2.54, null, null, true),
            ["official"] = new("official", "공문서", 3d, 1.5, 2d, 2d, null, null, 1.5, 1d, false),
        };

        public static IReadOnlyDictionary<string, WordPaperSize> Sizes { get; } = new Dictionary<string, WordPaperSize>(StringComparer.OrdinalIgnoreCase)
        {
            ["A3"] = new("A3", 16838, 23811, 29.7, 42d, 8),
            ["A4"] = new("A4", 11906, 16838, 21d, 29.7, 9),
            ["A5"] = new("A5", 8391, 11906, 14.8, 21d, 11),
            ["B4"] = new("B4", 14570, 20636, 25.7, 36.4, 12),
            ["B5"] = new("B5", 10319, 14570, 18.2, 25.7, 13),
            ["Letter"] = new("Letter", 12240, 15840, 21.59, 27.94, 1),
            ["Legal"] = new("Legal", 12240, 20160, 21.59, 35.56, 5),
            ["Tabloid"] = new("Tabloid", 15840, 24480, 27.94, 43.18, 3),
            ["Executive"] = new("Executive", 10440, 15120, 18.41, 26.67, 7),
        };

        public static IReadOnlyDictionary<string, WordColumnPreset> Columns { get; } = new Dictionary<string, WordColumnPreset>(StringComparer.OrdinalIgnoreCase)
        {
            ["one"] = new("one", 1, null, true, Array.Empty<int>()),
            ["two"] = new("two", 2, 1.27, true, Array.Empty<int>()),
            ["three"] = new("three", 3, 1.27, true, Array.Empty<int>()),
            ["left"] = new("left", 2, 1.27, false, new[] { 1, 2 }),
            ["right"] = new("right", 2, 1.27, false, new[] { 2, 1 }),
        };

        public static IReadOnlyDictionary<string, WordSpacingPreset> SpacingSets { get; } = new Dictionary<string, WordSpacingPreset>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new("default", "기본", 0d, 8d, 1.08),
            ["none"] = new("none", "단락 간격 없음", 0d, 0d, 1d),
            ["compact"] = new("compact", "촘촘하게", 0d, 4d, 1d),
            ["tight"] = new("tight", "좁게", 0d, 6d, 1.15),
            ["open"] = new("open", "열기", 0d, 10d, 1.15),
            ["relaxed"] = new("relaxed", "느슨하게", 0d, 6d, 1.5),
            ["double"] = new("double", "2배", 0d, 8d, 2d),
        };

        public static IReadOnlyDictionary<string, WordThemePreset> Themes { get; } = new Dictionary<string, WordThemePreset>(StringComparer.OrdinalIgnoreCase)
        {
            ["office"] = new("office", "Office", "office", "맑은 고딕", "default"),
            ["modern"] = new("modern", "모던", "teal", "맑은 고딕", "tight"),
            ["classic"] = new("classic", "클래식", "grayscale", "바탕", "default"),
            ["calm"] = new("calm", "차분한", "green", "맑은 고딕 · 바탕", "open"),
            ["vivid"] = new("vivid", "선명한", "orange", "맑은 고딕", "default"),
            ["mono"] = new("mono", "흑백", "grayscale", "맑은 고딕 · 바탕", "compact"),
        };

        public static IReadOnlyDictionary<string, WordColorScheme> ColorSchemes { get; } = new Dictionary<string, WordColorScheme>(StringComparer.OrdinalIgnoreCase)
        {
            ["office"] = new("office", "Office", "000000", "FFFFFF", "44546A", "E7E6E6", new[] { "4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47" }, "0563C1", "954F72"),
            ["teal"] = new("teal", "청록", "000000", "FFFFFF", "1F4E5A", "E3F1F2", new[] { "0B7285", "2F9E8F", "7BB6B6", "E9A23B", "3A7CA5", "8DAA5B" }, "0B7285", "6B5B95"),
            ["violet"] = new("violet", "보라", "000000", "FFFFFF", "3B2A66", "EEE9F7", new[] { "6F42C1", "A06AD0", "C7A7E6", "E39B3A", "4B6CB7", "5FA777" }, "6F42C1", "8E6BB5"),
            ["orange"] = new("orange", "주황", "000000", "FFFFFF", "5A3A22", "F8EBDD", new[] { "D9531E", "F2994A", "F2C94C", "8C6D46", "4F6D7A", "7A9E48" }, "C0501C", "8C6D46"),
            ["green"] = new("green", "초록", "000000", "FFFFFF", "1D3B2A", "E3F1E8", new[] { "1E7145", "4C9F70", "8FC1A0", "C9A227", "2F6F8F", "B5462A" }, "1E7145", "6B5B95"),
            ["grayscale"] = new("grayscale", "회색조", "000000", "FFFFFF", "404040", "EDEDED", new[] { "404040", "595959", "7F7F7F", "A6A6A6", "BFBFBF", "D9D9D9" }, "404040", "7F7F7F"),
        };

        public static IReadOnlyDictionary<string, WordFontSet> FontSets { get; } = new Dictionary<string, WordFontSet>(StringComparer.OrdinalIgnoreCase)
        {
            ["맑은 고딕"] = new("맑은 고딕", "맑은 고딕", "맑은 고딕", null, null),
            ["맑은 고딕 · 바탕"] = new("맑은 고딕 · 바탕", "맑은 고딕", "바탕", null, null),
            ["바탕"] = new("바탕", "바탕", "바탕", null, null),
            ["돋움 · 바탕"] = new("돋움 · 바탕", "돋움", "바탕", null, null),
            ["Arial · 맑은 고딕"] = new("Arial · 맑은 고딕", "Arial", "Arial", "맑은 고딕", "맑은 고딕"),
            ["Times New Roman · 바탕"] = new("Times New Roman · 바탕", "Times New Roman", "Times New Roman", "바탕", "바탕"),
        };

        public static IReadOnlyDictionary<string, WordCoverPreset> Covers { get; } = new Dictionary<string, WordCoverPreset>(StringComparer.OrdinalIgnoreCase)
        {
            ["classic"] = new("classic", "표준", "제목 56pt 굵게 가운데(위에서 35%), 부제 20pt, 아래쪽에 작성자·날짜 14pt 가운데"),
            ["banded"] = new("banded", "줄무늬", "위쪽 문단 음영 띠(accent1) 안에 흰색 제목 44pt, 아래에 부제·작성자·날짜 왼쪽 맞춤"),
            ["minimal"] = new("minimal", "간결", "왼쪽 맞춤, 제목 40pt, 위에 가는 선, 부제 16pt 회색"),
            ["bold"] = new("bold", "굵은 제목", "제목 72pt 굵게 왼쪽, 아래 굵은 선(6pt accent1), 나머지 12pt"),
            ["sidebar"] = new("sidebar", "사이드 막대", "문단 왼쪽 굵은 세로 선(24pt accent1), 제목 48pt, 부제·작성자·날짜"),
        };

        public static IReadOnlyDictionary<string, WordEnvelopePreset> Envelopes { get; } = new Dictionary<string, WordEnvelopePreset>(StringComparer.OrdinalIgnoreCase)
        {
            ["korean_small"] = new("korean_small", "소봉투", 220d, 105d),
            ["DL"] = new("DL", "DL", 220d, 110d),
            ["C5"] = new("C5", "C5", 229d, 162d),
            ["C6"] = new("C6", "C6", 162d, 114d),
            ["size10"] = new("size10", "10호", 241d, 105d),
        };

        public static IReadOnlyDictionary<string, WordLabelLayout> LabelLayouts { get; } = new Dictionary<string, WordLabelLayout>(StringComparer.OrdinalIgnoreCase)
        {
            ["A4_2x7"] = new("A4_2x7", 2, 7, 99.1, 38.1, 15.1, 4.65),
            ["A4_2x8"] = new("A4_2x8", 2, 8, 99.1, 33.9, 12.9, 4.65),
            ["A4_3x7"] = new("A4_3x7", 3, 7, 63.5, 38.1, 15.1, 7.2),
            ["A4_3x8"] = new("A4_3x8", 3, 8, 70d, 36d, 4.5, 0d),
            ["A4_3x10"] = new("A4_3x10", 3, 10, 70d, 29.7, 0d, 0d),
        };

        public static IReadOnlyList<string> Watermarks { get; } = new[] { "기밀", "초안", "사본", "긴급", "견본" };

        public static IReadOnlyList<double> FontSizes { get; } = new[] { 8d, 9d, 10d, 10.5, 11d, 12d, 14d, 16d, 18d, 20d, 22d, 24d, 26d, 28d, 36d, 48d, 72d };

        public static IReadOnlyList<string> HighlightNames { get; } = new[] { "yellow", "green", "cyan", "magenta", "blue", "red", "darkBlue", "darkCyan", "darkGreen", "darkMagenta", "darkRed", "darkYellow", "darkGray", "lightGray", "black", "white" };

        public static T Pick<T>(IReadOnlyDictionary<string, T> table, string key, string label, Func<T, string> names = null)
        {
            var text = (key ?? string.Empty).Trim();
            if (table.TryGetValue(text, out var found))
                return found;

            if (names != null)
            {
                foreach (var value in table.Values)
                {
                    if (string.Equals(names(value), text, StringComparison.OrdinalIgnoreCase))
                        return value;
                }
            }

            throw new ToolInputException($"'{label}' 값 ‘{text}’을(를) 알 수 없어요. 쓸 수 있는 값: {string.Join(", ", table.Keys)}");
        }
    }
}
