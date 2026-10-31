using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public sealed record PptThemePreset(string Id, string Name, IReadOnlyList<string> Colors, string HeadingFont, string BodyFont);

    public sealed record PptFontPreset(string Id, string Name, string HeadingFont, string BodyFont);

    public static class PptThemePresets
    {
        public static readonly IReadOnlyList<string> ThemeIds = new[] { "office", "ink", "forest", "ocean", "wine", "slate", "sunrise", "violet" };

        public static readonly IReadOnlyList<string> FontIds = new[] { "malgun", "serif_heading", "dotum" };

        private static readonly IReadOnlyDictionary<string, PptThemePreset> s_themes = new Dictionary<string, PptThemePreset>
        {
            ["office"] = Theme("office", "Office 테마", "000000", "FFFFFF", "44546A", "E7E6E6", "4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47", "0563C1", "954F72", "맑은 고딕", "맑은 고딕"),
            ["ink"] = Theme("ink", "문서 도우미 남색", "1C2A48", "FFFFFF", "4A5570", "EEF1F6", "1C2A48", "B5462A", "2F5FA8", "1E7145", "0B7285", "B59A00", "2F5FA8", "646E86", "맑은 고딕", "맑은 고딕"),
            ["forest"] = Theme("forest", "숲", "1B2A22", "FFFFFF", "2E4A3A", "EDF3EA", "2E7D32", "8D6E63", "66BB6A", "F9A825", "00796B", "5D4037", "1565C0", "6A1B9A", "바탕", "맑은 고딕"),
            ["ocean"] = Theme("ocean", "바다", "0D2B45", "FFFFFF", "1A4A6E", "E8F1F8", "0277BD", "00ACC1", "1565C0", "26A69A", "5C6BC0", "78909C", "0D47A1", "6A1B9A", "맑은 고딕", "맑은 고딕"),
            ["wine"] = Theme("wine", "와인", "2B1216", "FFFFFF", "5A1E2B", "F6EBEC", "8E2430", "D9534F", "F0AD4E", "6D4C41", "455A64", "AD1457", "8E2430", "6D4C41", "바탕", "맑은 고딕"),
            ["slate"] = Theme("slate", "회색 돌", "1F2A30", "FFFFFF", "37474F", "ECEFF1", "455A64", "78909C", "90A4AE", "546E7A", "26A69A", "F57C00", "1565C0", "6A1B9A", "맑은 고딕", "맑은 고딕"),
            ["sunrise"] = Theme("sunrise", "노을", "3E2723", "FFFFFF", "5D4037", "FBE9E7", "E65100", "FB8C00", "FFB300", "F4511E", "8D6E63", "6D4C41", "1565C0", "6A1B9A", "맑은 고딕", "맑은 고딕"),
            ["violet"] = Theme("violet", "보라", "1F1635", "FFFFFF", "311B92", "EDE7F6", "5E35B1", "7E57C2", "9575CD", "3949AB", "00897B", "F9A825", "311B92", "6A1B9A", "맑은 고딕", "맑은 고딕"),
        };

        private static readonly IReadOnlyDictionary<string, PptFontPreset> s_fonts = new Dictionary<string, PptFontPreset>
        {
            ["malgun"] = new("malgun", "맑은 고딕", "맑은 고딕", "맑은 고딕"),
            ["serif_heading"] = new("serif_heading", "바탕 제목 + 맑은 고딕 본문", "바탕", "맑은 고딕"),
            ["dotum"] = new("dotum", "돋움", "돋움", "돋움"),
        };

        public static PptThemePreset Theme(string id) => s_themes[id];

        public static PptFontPreset Font(string id) => s_fonts[id];

        private static PptThemePreset Theme(string id, string name, params string[] values) =>
            new(id, name, values[..12], values[12], values[13]);
    }
}
