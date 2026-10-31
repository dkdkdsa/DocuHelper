using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocuHelper.Importers.Hwp;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class SetDocumentDesignTool : WordTool
    {
        private const int MaxHeadingAnchors = 20;

        private static readonly string[] s_fontKeys = { "heading", "body", "heading_east_asian", "body_east_asian" };

        public override string Name => "set_document_design";

        public override string Description =>
            "Change the overall design of the document: theme (colors + fonts + paragraph spacing together), color scheme, heading/body fonts, or the default paragraph spacing. "
            + "Text with a font or color set directly on it keeps it. Use presets by name or give custom fonts and colors.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "theme": {"type": "string", "enum": ["office", "modern", "classic", "calm", "vivid", "mono"]},
                "colors": {
                  "description": "Preset name or custom scheme.",
                  "oneOf": [
                    {"type": "string", "enum": ["office", "teal", "violet", "orange", "green", "grayscale"]},
                    {"type": "object", "description": "dark1, light1, dark2, light2, accent1..accent6, hyperlink, followed_hyperlink as #RRGGBB.", "additionalProperties": {"type": "string"}}
                  ]
                },
                "fonts": {
                  "type": "object",
                  "properties": {
                    "heading": {"type": "string"},
                    "body": {"type": "string"},
                    "heading_east_asian": {"type": "string"},
                    "body_east_asian": {"type": "string"}
                  }
                },
                "paragraph_spacing": {"type": "string", "enum": ["default", "none", "compact", "tight", "open", "relaxed", "double"]},
                "apply_to_styles": {"type": "boolean", "description": "Also rewrite Normal, Title, headings, captions and hyperlinks to follow the theme. Default true."}
              }
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.Partial;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var request = Request.Parse(input);
            RefuseInHwp(docu, "테마와 색 구성표", request.Theme != null || request.Colors != null);
            var scan = WordScan.Read(docu.Document);
            var snapshot = docu.Capture();
            try
            {
                return Apply(docu, scan, request);
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static ToolResult Apply(WordDocu docu, WordScan scan, Request request)
        {
            var themed = docu is not HwpDocu;
            var before = WordTheme.FromDocument(docu.Document, scan.Styles);
            var after = Clone(before);
            var fontsChanged = false;
            var colorsChanged = false;
            string colorsName = null;
            string fontsName = null;
            string spacingName = null;

            var preset = request.Theme == null ? null : WordPresets.Pick(WordPresets.Themes, request.Theme, "theme", t => t.Label);
            if (preset != null)
            {
                colorsName = preset.Colors;
                fontsName = preset.Fonts;
                spacingName = preset.Spacing;
            }

            if (request.Colors != null && request.Colors.Preset != null)
                colorsName = request.Colors.Preset;

            if (request.Spacing != null)
                spacingName = request.Spacing;

            if (colorsName != null)
            {
                var scheme = WordPresets.Pick(WordPresets.ColorSchemes, colorsName, "colors", s => s.Label);
                after.Colors = WordTheme.Flatten(scheme);
                after.ColorSchemeName = scheme.Label;
                colorsChanged = true;
            }

            if (request.Colors?.Custom != null)
            {
                foreach (var pair in request.Colors.Custom)
                    after.Colors[pair.Key] = pair.Value;

                after.ColorSchemeName = "Custom";
                colorsChanged = true;
            }

            if (fontsName != null)
            {
                var set = WordPresets.Pick(WordPresets.FontSets, fontsName, "fonts");
                Fonts(after, set.Heading, set.Body, set.HeadingEastAsian, set.BodyEastAsian);
                after.FontSchemeName = set.Key;
                fontsChanged = true;
            }

            if (request.Fonts != null)
            {
                Fonts(after, request.Fonts.GetValueOrDefault("heading"), request.Fonts.GetValueOrDefault("body"), request.Fonts.GetValueOrDefault("heading_east_asian"), request.Fonts.GetValueOrDefault("body_east_asian"));
                after.FontSchemeName = "Custom";
                fontsChanged = true;
            }

            if (themed && (colorsChanged || fontsChanged))
            {
                var part = WordThemeXml.Ensure(docu);
                WordTheme.Write(part, after);
                EnsureMapping(docu);
            }

            if (request.ApplyToStyles || !themed)
            {
                if (fontsChanged)
                    WordThemeStyler.Fonts(scan, before, after, themed);

                if (colorsChanged && themed)
                    WordThemeStyler.Colors(scan, before, after);
            }

            if (spacingName != null)
                WordThemeStyler.Spacing(scan, WordPresets.Pick(WordPresets.SpacingSets, spacingName, "paragraph_spacing", s => s.Label));

            var fresh = WordScan.Read(docu.Document);
            var anchors = new List<string> { WordAnchors.Doc("design") };
            anchors.AddRange(fresh.Blocks.Where(b => !b.IsTable && b.IsHeading).Take(MaxHeadingAnchors).Select(b => b.Anchor));

            var parts = new List<string>();
            var details = new List<string>();
            if (preset != null)
            {
                parts.Add($"theme {preset.Key}");
                details.Add("테마 바꿈");
            }

            if (colorsChanged)
            {
                parts.Add($"colors {(colorsName ?? "custom")}");
                if (preset == null)
                    details.Add("색 바꿈");
            }

            if (fontsChanged)
            {
                parts.Add($"fonts heading {after.MajorLatin}/{after.MajorEastAsian}, body {after.MinorLatin}/{after.MinorEastAsian}");
                if (preset == null)
                    details.Add("글꼴 바꿈");
            }

            if (spacingName != null)
            {
                parts.Add($"paragraph spacing {spacingName}");
                if (preset == null)
                    details.Add("단락 간격");
            }

            var styles = request.ApplyToStyles || !themed ? "styles updated" : "styles left as they were";
            return ToolResult.Changed(
                $"Set the document design: {string.Join("; ", parts)}; {styles}.",
                Change("문서 전체", "type", anchors),
                string.Join(", ", details));
        }

        private static void Fonts(WordTheme theme, string heading, string body, string headingEast, string bodyEast)
        {
            if (heading != null)
            {
                theme.MajorLatin = heading;
                theme.MajorEastAsian = headingEast ?? (WordTheme.IsKorean(heading) ? heading : theme.MajorEastAsian);
            }
            else if (headingEast != null)
            {
                theme.MajorEastAsian = headingEast;
            }

            if (body != null)
            {
                theme.MinorLatin = body;
                theme.MinorEastAsian = bodyEast ?? (WordTheme.IsKorean(body) ? body : theme.MinorEastAsian);
            }
            else if (bodyEast != null)
            {
                theme.MinorEastAsian = bodyEast;
            }
        }

        private static WordTheme Clone(WordTheme theme) => new()
        {
            ColorSchemeName = theme.ColorSchemeName,
            FontSchemeName = theme.FontSchemeName,
            MajorLatin = theme.MajorLatin,
            MajorEastAsian = theme.MajorEastAsian,
            MinorLatin = theme.MinorLatin,
            MinorEastAsian = theme.MinorEastAsian,
            Colors = new Dictionary<string, string>(theme.Colors),
        };

        private static void EnsureMapping(WordDocu docu)
        {
            var settings = WordParts.EnsureSettings(docu);
            if (settings.GetFirstChild<ColorSchemeMapping>() != null)
                return;

            settings.AddChild(new ColorSchemeMapping
            {
                Background1 = ColorSchemeIndexValues.Light1,
                Text1 = ColorSchemeIndexValues.Dark1,
                Background2 = ColorSchemeIndexValues.Light2,
                Text2 = ColorSchemeIndexValues.Dark2,
                Accent1 = ColorSchemeIndexValues.Accent1,
                Accent2 = ColorSchemeIndexValues.Accent2,
                Accent3 = ColorSchemeIndexValues.Accent3,
                Accent4 = ColorSchemeIndexValues.Accent4,
                Accent5 = ColorSchemeIndexValues.Accent5,
                Accent6 = ColorSchemeIndexValues.Accent6,
                Hyperlink = ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = ColorSchemeIndexValues.FollowedHyperlink,
            });
        }

        private sealed class Colors
        {
            public string Preset { get; set; }

            public Dictionary<string, string> Custom { get; set; }
        }

        private sealed class Request
        {
            public string Theme { get; private set; }

            public Colors Colors { get; private set; }

            public Dictionary<string, string> Fonts { get; private set; }

            public string Spacing { get; private set; }

            public bool ApplyToStyles { get; private set; } = true;

            public static Request Parse(ToolInput input)
            {
                var request = new Request();
                request.Theme = Text(input, "theme");
                request.Spacing = Text(input, "paragraph_spacing");
                request.ApplyToStyles = input.GetOptionalBool("apply_to_styles") ?? true;

                if (input.Has("colors"))
                {
                    var element = input.Get("colors");
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        var name = element.GetString()?.Trim();
                        if (string.IsNullOrEmpty(name))
                            throw new ToolInputException("'colors' 값이 비어 있어요.");

                        request.Colors = new Colors { Preset = name };
                    }
                    else if (element.ValueKind == JsonValueKind.Object)
                    {
                        request.Colors = new Colors { Custom = CustomColors(element) };
                    }
                    else
                    {
                        throw new ToolInputException("'colors' 값은 색 구성표 이름이거나 색 목록이어야 해요.");
                    }
                }

                if (input.Has("fonts"))
                    request.Fonts = FontNames(input.Get("fonts"));

                if (request.Theme == null && request.Colors == null && request.Fonts == null && request.Spacing == null)
                    throw new ToolInputException("바꿀 것을 하나 이상 넣어 주세요. theme, colors, fonts, paragraph_spacing 중에서 골라 주세요.");

                if (request.Theme != null)
                    WordPresets.Pick(WordPresets.Themes, request.Theme, "theme", t => t.Label);

                if (request.Spacing != null)
                    WordPresets.Pick(WordPresets.SpacingSets, request.Spacing, "paragraph_spacing", s => s.Label);

                if (request.Colors?.Preset != null)
                    WordPresets.Pick(WordPresets.ColorSchemes, request.Colors.Preset, "colors", s => s.Label);

                return request;
            }

            private static string Text(ToolInput input, string name)
            {
                if (!input.Has(name))
                    return null;

                var value = input.GetOptionalString(name)?.Trim();
                if (string.IsNullOrEmpty(value))
                    throw new ToolInputException($"'{name}' 값이 비어 있어요.");

                return value;
            }

            private static Dictionary<string, string> CustomColors(JsonElement element)
            {
                var colors = new Dictionary<string, string>();
                foreach (var property in element.EnumerateObject())
                {
                    var key = property.Name.Trim().ToLowerInvariant();
                    if (!WordTheme.ColorKeys.Contains(key))
                        throw new ToolInputException($"'colors'에 ‘{property.Name}’은(는) 쓸 수 없어요. 쓸 수 있는 값: {string.Join(", ", WordTheme.ColorKeys)}");

                    var text = ToolInput.AsText(property.Value) ?? string.Empty;
                    var hex = text.Trim().TrimStart('#');
                    if (hex.Length != 6 || !hex.All(Uri.IsHexDigit))
                        throw new ToolInputException($"'colors.{property.Name}' 값은 #RRGGBB 형식의 색이어야 해요. 예: #0B7285");

                    colors[key] = hex.ToUpperInvariant();
                }

                if (colors.Count == 0)
                    throw new ToolInputException("'colors'에 바꿀 색을 하나 이상 넣어 주세요.");

                return colors;
            }

            private static Dictionary<string, string> FontNames(JsonElement element)
            {
                if (element.ValueKind != JsonValueKind.Object)
                    throw new ToolInputException("'fonts' 값은 {\"heading\": \"맑은 고딕\", \"body\": \"바탕\"} 형식이어야 해요.");

                var fonts = new Dictionary<string, string>();
                foreach (var property in element.EnumerateObject())
                {
                    if (!s_fontKeys.Contains(property.Name))
                        throw new ToolInputException($"'fonts'에 ‘{property.Name}’은(는) 쓸 수 없어요. 쓸 수 있는 값: {string.Join(", ", s_fontKeys)}");

                    var name = (ToolInput.AsText(property.Value) ?? string.Empty).Trim();
                    if (name.Length == 0 || name.Length > 64 || name.Any(char.IsControl))
                        throw new ToolInputException($"'fonts.{property.Name}' 값은 글꼴 이름이어야 해요. 예: 맑은 고딕");

                    fonts[property.Name] = name;
                }

                if (fonts.Count == 0)
                    throw new ToolInputException("'fonts'에 바꿀 글꼴을 하나 이상 넣어 주세요.");

                return fonts;
            }
        }
    }
}
