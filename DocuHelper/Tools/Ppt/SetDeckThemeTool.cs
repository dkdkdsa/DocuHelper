using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Ppt
{
    public class SetDeckThemeTool : Tool
    {
        private const int MaxFontLength = 60;

        private static readonly Regex s_hex = new(@"^#?[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant);

        private static readonly string[] s_slotNames =
        {
            "dark1", "light1", "dark2", "light2", "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "hyperlink", "followed_hyperlink",
        };

        public override string Name => "set_deck_theme";

        public override string Description =>
            "Change the look of the whole deck through theme colors and fonts (layouts and content stay). "
            + "`theme` applies a ready-made set: office, ink (navy + brick red), forest, ocean, wine, slate, sunrise, violet. "
            + "`colors` applies only colors (preset id, or an object of slots dark1 light1 dark2 light2 accent1-accent6 hyperlink followed_hyperlink as #RRGGBB); "
            + "`fonts` only fonts (malgun, serif_heading, dotum, or {heading, body}); `background_style` light or dark. "
            + "Colors and fonts set explicitly on a shape or run do not change.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "theme": {"type": "string", "enum": ["office", "ink", "forest", "ocean", "wine", "slate", "sunrise", "violet"]},
                "colors": {
                  "anyOf": [
                    {"type": "string", "enum": ["office", "ink", "forest", "ocean", "wine", "slate", "sunrise", "violet"]},
                    {"type": "object", "additionalProperties": {"type": "string"}}
                  ]
                },
                "fonts": {
                  "anyOf": [
                    {"type": "string", "enum": ["malgun", "serif_heading", "dotum"]},
                    {"type": "object", "properties": {"heading": {"type": "string"}, "body": {"type": "string"}}}
                  ]
                },
                "background_style": {"type": "string", "enum": ["light", "dark"]}
              }
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var presentation = document.PresentationPart
                ?? throw new ToolInputException("이 파일은 슬라이드 문서로 열 수 없어요.");
            var theme = PptInputs.Choice(input, "theme", PptThemePresets.ThemeIds.ToArray());
            var background = PptInputs.Choice(input, "background_style", "light", "dark");
            if (theme == null && !input.Has("colors") && !input.Has("fonts") && background == null)
                throw new ToolInputException("바꿀 테마(theme), 색(colors), 글꼴(fonts), 배경 스타일(background_style) 중 하나를 알려 주세요.");

            var themePreset = theme == null ? null : PptThemePresets.Theme(theme);
            var colors = ColorsFor(input, themePreset, out var colorName, out var colorOverrides);
            var fonts = FontsFor(input, themePreset, out var fontName, out var heading, out var body, out var latinOnly);
            var themeName = themePreset?.Name;

            var snapshot = docu.Capture();
            try
            {
                foreach (var master in presentation.SlideMasterParts)
                {
                    var themePart = master.ThemePart?.Theme;
                    if (themePart != null)
                    {
                        if (colors != null)
                            PptThemeWriter.Colors(themePart, colorName, colors);
                        else if (colorOverrides != null)
                            PptThemeWriter.Colors(themePart, colorName, MasterColors(PptTheme.Read(themePart).Colors, colorOverrides));

                        if (fonts)
                            PptThemeWriter.Fonts(themePart, fontName, heading, body, latinOnly);

                        if (themeName != null)
                            themePart.Name = themeName;
                    }

                    if (background != null)
                        PptThemeWriter.Background(master.SlideMaster, background == "dark");
                }

                var slides = PptSlides.List(document);
                var parts = new List<string>();
                if (themeName != null)
                    parts.Add($"applied the {themeName} theme");

                if (colors != null && themeName == null)
                    parts.Add($"set the colors to {colorName}");

                if (fonts && themeName == null)
                    parts.Add($"set the fonts to {heading ?? "unchanged"} / {body ?? "unchanged"}");

                if (background != null)
                    parts.Add($"set the {background} background style");

                var change = new DocuChange
                {
                    Location = "전체",
                    Icon = "theme",
                };
                change.Anchors.AddRange(slides.Select(s => PptAnchor.Slide(s.SlideId)));
                var detail = themeName != null ? $"테마 {themeName}"
                    : colors != null ? "색 바꿈"
                    : fonts ? "글꼴 바꿈"
                    : background == "dark" ? "어두운 배경" : "밝은 배경";
                return ToolResult.Changed($"Deck {string.Join(", ", parts)}; layouts and content stay as they are.", change, detail);
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static IReadOnlyList<string> ColorsFor(ToolInput input, PptThemePreset themePreset, out string name, out IReadOnlyDictionary<int, string> overrides)
        {
            name = themePreset?.Name ?? "사용자 지정";
            overrides = null;
            if (!input.Has("colors"))
                return themePreset?.Colors;

            var element = input.Get("colors");
            if (element.ValueKind == JsonValueKind.String)
            {
                var id = PptInputs.Choice(input, "colors", PptThemePresets.ThemeIds.ToArray())
                    ?? throw new ToolInputException("'colors'는 색 세트 이름이나 색 목록이어야 해요.");
                var preset = PptThemePresets.Theme(id);
                name = preset.Name;
                return preset.Colors;
            }

            if (element.ValueKind != JsonValueKind.Object)
                throw new ToolInputException("'colors'는 색 세트 이름이나 색 목록이어야 해요.");

            var changes = new Dictionary<int, string>();
            foreach (var property in element.EnumerateObject())
            {
                var index = Array.IndexOf(s_slotNames, property.Name);
                if (index < 0)
                    throw new ToolInputException($"'colors'에 없는 색 이름 '{property.Name}'이 있어요. dark1, light1, dark2, light2, accent1~accent6, hyperlink, followed_hyperlink만 쓸 수 있어요.");

                var text = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                if (text == null || !s_hex.IsMatch(text.Trim()))
                    throw new ToolInputException($"'colors.{property.Name}' 값은 #RRGGBB 형식의 색이어야 해요.");

                changes[index] = Normalize(text);
            }

            if (themePreset != null)
                return MasterColors(themePreset.Colors, changes);

            overrides = changes;
            return null;
        }

        private static IReadOnlyList<string> MasterColors(IEnumerable<string> current, IReadOnlyDictionary<int, string> changes)
        {
            var result = current.Select(Normalize).ToList();
            foreach (var change in changes)
                result[change.Key] = change.Value;

            return result;
        }

        private static bool FontsFor(ToolInput input, PptThemePreset themePreset, out string name, out string heading, out string body, out bool latinOnly)
        {
            heading = themePreset?.HeadingFont;
            body = themePreset?.BodyFont;
            latinOnly = false;
            name = themePreset == null ? "사용자 지정" : themePreset.Name;
            if (!input.Has("fonts"))
                return false;

            var element = input.Get("fonts");
            if (element.ValueKind == JsonValueKind.String)
            {
                var id = PptInputs.Choice(input, "fonts", PptThemePresets.FontIds.ToArray())
                    ?? throw new ToolInputException("'fonts'는 글꼴 세트 이름이나 제목과 본문 글꼴이어야 해요.");
                var preset = PptThemePresets.Font(id);
                name = preset.Name;
                heading = preset.HeadingFont;
                body = preset.BodyFont;
                return true;
            }

            var fonts = PptInputs.Object(input, "fonts")
                ?? throw new ToolInputException("'fonts'는 글꼴 세트 이름이나 제목과 본문 글꼴이어야 해요.");
            heading = PptInputs.Text(fonts, "heading", MaxFontLength);
            body = PptInputs.Text(fonts, "body", MaxFontLength);
            latinOnly = true;
            name = "사용자 지정";
            return true;
        }

        private static string Normalize(string hex) => hex.Trim().TrimStart('#').ToUpperInvariant();
    }
}
