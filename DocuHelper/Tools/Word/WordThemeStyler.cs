using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordThemeStyler
    {
        private static readonly HashSet<string> s_headings = new(StringComparer.Ordinal)
        {
            "Title", "Heading1", "Heading2", "Heading3", "Heading4", "Heading5", "Heading6", "Heading7", "Heading8", "Heading9",
        };

        public static void Fonts(WordScan scan, WordTheme before, WordTheme after, bool themed)
        {
            var root = scan.Styles.Root;
            if (root == null)
                return;

            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                before.MajorLatin, before.MajorEastAsian, before.MinorLatin, before.MinorEastAsian,
            };

            var defaults = root.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle?.GetFirstChild<RunFonts>();
            foreach (var name in new[] { defaults?.Ascii?.Value, defaults?.HighAnsi?.Value, defaults?.EastAsia?.Value, defaults?.ComplexScript?.Value })
            {
                if (!string.IsNullOrEmpty(name))
                    known.Add(name);
            }

            var docDefaults = root.DocDefaults ??= new DocDefaults();
            var runDefault = docDefaults.RunPropertiesDefault ??= new RunPropertiesDefault();
            var baseStyle = runDefault.RunPropertiesBaseStyle ??= new RunPropertiesBaseStyle();
            Replace(baseStyle, false, after, themed);

            foreach (var style in root.Elements<Style>().Where(IsTextStyle))
            {
                var heading = IsHeading(style);
                var fonts = style.StyleRunProperties?.RunFonts;
                if (fonts != null)
                {
                    if (IsDefaultish(fonts, known))
                        Replace(style.StyleRunProperties, heading, after, themed);

                    continue;
                }

                if (heading)
                {
                    var properties = style.StyleRunProperties ?? new StyleRunProperties();
                    Replace(properties, true, after, themed);
                    if (properties.Parent == null)
                        style.StyleRunProperties = properties;
                }
            }
        }

        public static void Colors(WordScan scan, WordTheme before, WordTheme after)
        {
            var root = scan.Styles.Root;
            if (root == null)
                return;

            foreach (var style in root.Elements<Style>().Where(IsTextStyle))
            {
                var builtin = WordBuiltinStyles.Find(style.StyleName?.Val?.Value)?.Id;
                var target = Target(builtin);
                var color = style.StyleRunProperties?.Color;
                if (color == null)
                {
                    if (target == null || !s_headings.Contains(builtin))
                        continue;

                    var properties = style.StyleRunProperties ?? new StyleRunProperties();
                    properties.AddChild(Make(after, target.Value));
                    if (properties.Parent == null)
                        style.StyleRunProperties = properties;

                    continue;
                }

                if (color.ThemeColor?.Value != null)
                {
                    var resolved = after.Resolve(color);
                    if (resolved != null)
                        color.Val = resolved;

                    continue;
                }

                if (target != null && Matches(before, target.Value, color.Val?.Value))
                {
                    var properties = style.StyleRunProperties;
                    properties.RemoveAllChildren<Color>();
                    properties.AddChild(Make(after, target.Value));
                }
            }
        }

        public static void Spacing(WordScan scan, WordSpacingPreset preset)
        {
            var root = scan.Styles.Root;
            var docDefaults = root.DocDefaults ??= new DocDefaults();
            var paragraphDefault = docDefaults.ParagraphPropertiesDefault ??= new ParagraphPropertiesDefault();
            var baseStyle = paragraphDefault.ParagraphPropertiesBaseStyle ??= new ParagraphPropertiesBaseStyle();
            baseStyle.RemoveAllChildren<SpacingBetweenLines>();
            var spacing = new SpacingBetweenLines
            {
                After = WordUnits.PtToTwips(preset.AfterPt).ToString(CultureInfo.InvariantCulture),
                Line = WordUnits.LinesToTwentyFourths(preset.Line).ToString(CultureInfo.InvariantCulture),
                LineRule = LineSpacingRuleValues.Auto,
            };

            if (preset.BeforePt > 0)
                spacing.Before = WordUnits.PtToTwips(preset.BeforePt).ToString(CultureInfo.InvariantCulture);

            baseStyle.AddChild(spacing);
        }

        public static bool IsHeading(Style style)
        {
            var id = WordBuiltinStyles.Find(style.StyleName?.Val?.Value)?.Id;
            return id != null && s_headings.Contains(id);
        }

        private static bool IsTextStyle(Style style) => style.Type?.Value == StyleValues.Paragraph || style.Type?.Value == StyleValues.Character;

        private static bool IsDefaultish(RunFonts fonts, HashSet<string> known)
        {
            var names = new[] { fonts.Ascii?.Value, fonts.HighAnsi?.Value, fonts.EastAsia?.Value, fonts.ComplexScript?.Value };
            var themed = fonts.AsciiTheme != null || fonts.HighAnsiTheme != null || fonts.EastAsiaTheme != null || fonts.ComplexScriptTheme != null;
            var explicitNames = names.Where(n => !string.IsNullOrEmpty(n)).ToList();
            return themed || (explicitNames.Count > 0 && explicitNames.All(known.Contains));
        }

        private static void Replace(DocumentFormat.OpenXml.OpenXmlCompositeElement properties, bool heading, WordTheme after, bool themed)
        {
            var hint = properties.GetFirstChild<RunFonts>()?.Hint?.Value;
            properties.RemoveAllChildren<RunFonts>();
            RunFonts fonts;
            if (themed)
            {
                fonts = new RunFonts
                {
                    AsciiTheme = heading ? ThemeFontValues.MajorHighAnsi : ThemeFontValues.MinorHighAnsi,
                    HighAnsiTheme = heading ? ThemeFontValues.MajorHighAnsi : ThemeFontValues.MinorHighAnsi,
                    EastAsiaTheme = heading ? ThemeFontValues.MajorEastAsia : ThemeFontValues.MinorEastAsia,
                    ComplexScriptTheme = heading ? ThemeFontValues.MajorBidi : ThemeFontValues.MinorBidi,
                };
            }
            else
            {
                var latin = heading ? after.MajorLatin : after.MinorLatin;
                var east = heading ? after.MajorEastAsian : after.MinorEastAsian;
                fonts = new RunFonts { Ascii = latin, HighAnsi = latin, EastAsia = east, ComplexScript = latin };
            }

            if (hint != null)
                fonts.Hint = hint;

            properties.AddChild(fonts);
        }

        private static (ThemeColorValues Theme, string Shade)? Target(string builtin)
        {
            switch (builtin)
            {
                case "Title":
                case "Caption":
                    return (ThemeColorValues.Text2, null);
                case "Heading3":
                    return (ThemeColorValues.Accent1, "7F");
                case "Hyperlink":
                    return (ThemeColorValues.Hyperlink, null);
                case "FollowedHyperlink":
                    return (ThemeColorValues.FollowedHyperlink, null);
                default:
                    return builtin != null && s_headings.Contains(builtin) ? (ThemeColorValues.Accent1, "BF") : null;
            }
        }

        private static Color Make(WordTheme theme, (ThemeColorValues Theme, string Shade) target)
        {
            var color = new Color { ThemeColor = target.Theme };
            if (target.Shade != null)
                color.ThemeShade = target.Shade;

            color.Val = theme.Resolve(color) ?? "auto";
            return color;
        }

        private static bool Matches(WordTheme theme, (ThemeColorValues Theme, string Shade) target, string value)
        {
            if (string.IsNullOrEmpty(value) || value == "auto")
                return true;

            var probe = new Color { ThemeColor = target.Theme };
            if (target.Shade != null)
                probe.ThemeShade = target.Shade;

            var plain = theme.Color(target.Theme);
            return string.Equals(theme.Resolve(probe), value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(plain, value, StringComparison.OrdinalIgnoreCase);
        }
    }
}
