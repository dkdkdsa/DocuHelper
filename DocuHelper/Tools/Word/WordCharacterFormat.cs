using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordCharacterFormat
    {
        public static readonly IReadOnlyList<string> Keys = new[]
        {
            "font", "size", "bold", "italic", "underline", "strike", "color", "highlight", "vertical_align", "caps", "spacing_pt",
        };

        public static WordCharacterSpec Parse(ToolInput input, string label)
        {
            if (input.Root.ValueKind != JsonValueKind.Object)
                throw new ToolInputException($"'{label}' 값은 속성 목록이어야 해요.");

            if (input.Root.EnumerateObject().Count() > WordParagraphFormat.MaxStyleProperties)
                throw new ToolInputException($"'{label}'에는 속성을 {WordParagraphFormat.MaxStyleProperties}개까지만 넣을 수 있어요.");

            foreach (var property in input.Root.EnumerateObject())
            {
                if (!Keys.Contains(property.Name))
                    throw new ToolInputException($"'{label}'에 ‘{property.Name}’은(는) 쓸 수 없어요. 쓸 수 있는 값: {string.Join(", ", Keys)}");
            }

            var spec = new WordCharacterSpec();
            if (input.Has("font"))
            {
                var font = (input.GetOptionalString("font") ?? string.Empty).Trim();
                if (font.Length == 0 || font.Length > 64 || font.Any(char.IsControl))
                    throw new ToolInputException("'font' 값은 글꼴 이름이어야 해요. 예: 맑은 고딕");

                spec.Font = font;
            }

            var size = input.GetOptionalNumber("size");
            if (size != null)
                spec.Size = WordUnits.FontSizeHalfPoints(WordUnits.Check(size.Value, 1, 400, "size", "pt"), "size");

            spec.Bold = input.GetOptionalBool("bold");
            spec.Italic = input.GetOptionalBool("italic");
            spec.Underline = ParseUnderline(input.GetOptionalString("underline"));
            spec.Strike = Pick(input.GetOptionalString("strike"), "strike", "none", "single", "double");
            spec.Caps = Pick(input.GetOptionalString("caps"), "caps", "none", "all", "small");

            var color = input.GetOptionalString("color");
            if (color != null)
                spec.Color = color.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase) ? "auto" : Hex(color, "color");

            var highlight = input.GetOptionalString("highlight");
            if (highlight != null)
                spec.Highlight = ParseHighlight(highlight);

            var vertical = Pick(input.GetOptionalString("vertical_align"), "vertical_align", "baseline", "superscript", "subscript");
            if (vertical != null)
                spec.VerticalAlign = vertical == "superscript" ? VerticalPositionValues.Superscript : vertical == "subscript" ? VerticalPositionValues.Subscript : VerticalPositionValues.Baseline;

            var spacing = input.GetOptionalNumber("spacing_pt");
            if (spacing != null)
                spec.Spacing = WordUnits.PtToTwips(WordUnits.Check(spacing.Value, -5, 20, "spacing_pt", "pt"));

            return spec;
        }

        public static void Apply(OpenXmlCompositeElement properties, WordCharacterSpec spec, WordInheritedRun inherited)
        {
            if (spec.Font != null)
            {
                var hint = properties.GetFirstChild<RunFonts>()?.Hint?.Value;
                properties.RemoveAllChildren<RunFonts>();
                var fonts = new RunFonts { Ascii = spec.Font, HighAnsi = spec.Font, EastAsia = spec.Font, ComplexScript = spec.Font };
                if (hint != null)
                    fonts.Hint = hint;

                properties.AddChild(fonts);
            }

            if (spec.Size != null)
            {
                properties.RemoveAllChildren<FontSize>();
                properties.RemoveAllChildren<FontSizeComplexScript>();
                var size = spec.Size.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                properties.AddChild(new FontSize { Val = size });
                properties.AddChild(new FontSizeComplexScript { Val = size });
            }

            if (spec.Bold != null)
            {
                Flag<Bold>(properties, spec.Bold.Value, inherited);
                Flag<BoldComplexScript>(properties, spec.Bold.Value, inherited);
            }

            if (spec.Italic != null)
            {
                Flag<Italic>(properties, spec.Italic.Value, inherited);
                Flag<ItalicComplexScript>(properties, spec.Italic.Value, inherited);
            }

            if (spec.Underline != null)
            {
                properties.RemoveAllChildren<Underline>();
                if (spec.Underline.Value != UnderlineValues.None)
                    properties.AddChild(new Underline { Val = spec.Underline.Value });
                else if (WordStyles.IsUnderlined(inherited.Element<Underline>()))
                    properties.AddChild(new Underline { Val = UnderlineValues.None });
            }

            if (spec.Strike != null)
            {
                properties.RemoveAllChildren<Strike>();
                properties.RemoveAllChildren<DoubleStrike>();
                if (spec.Strike == "single")
                {
                    properties.AddChild(new Strike());
                }
                else if (spec.Strike == "double")
                {
                    properties.AddChild(new DoubleStrike());
                }
                else
                {
                    if (inherited.IsOn<Strike>())
                        properties.AddChild(new Strike { Val = false });

                    if (inherited.IsOn<DoubleStrike>())
                        properties.AddChild(new DoubleStrike { Val = false });
                }
            }

            if (spec.Color != null)
            {
                properties.RemoveAllChildren<Color>();
                if (spec.Color != "auto")
                    properties.AddChild(new Color { Val = spec.Color });
                else if (inherited.Element<Color>() != null)
                    properties.AddChild(new Color { Val = "auto" });
            }

            if (spec.Highlight != null)
                SetHighlight(properties, spec.Highlight, inherited);

            if (spec.VerticalAlign != null)
            {
                properties.RemoveAllChildren<VerticalTextAlignment>();
                var inheritedAlign = inherited.Element<VerticalTextAlignment>()?.Val?.Value;
                if (spec.VerticalAlign.Value != VerticalPositionValues.Baseline)
                    properties.AddChild(new VerticalTextAlignment { Val = spec.VerticalAlign.Value });
                else if (inheritedAlign != null && inheritedAlign.Value != VerticalPositionValues.Baseline)
                    properties.AddChild(new VerticalTextAlignment { Val = VerticalPositionValues.Baseline });
            }

            if (spec.Caps != null)
            {
                properties.RemoveAllChildren<Caps>();
                properties.RemoveAllChildren<SmallCaps>();
                if (spec.Caps == "all")
                {
                    properties.AddChild(new Caps());
                }
                else if (spec.Caps == "small")
                {
                    properties.AddChild(new SmallCaps());
                }
                else
                {
                    if (inherited.IsOn<Caps>())
                        properties.AddChild(new Caps { Val = false });

                    if (inherited.IsOn<SmallCaps>())
                        properties.AddChild(new SmallCaps { Val = false });
                }
            }

            if (spec.Spacing != null)
            {
                properties.RemoveAllChildren<Spacing>();
                var present = inherited.Element<Spacing>()?.Val?.Value ?? 0;
                if (spec.Spacing.Value != 0 || present != 0)
                    properties.AddChild(new Spacing { Val = spec.Spacing.Value });
            }
        }

        public static void Merge(OpenXmlCompositeElement target, OpenXmlCompositeElement source)
        {
            foreach (var child in source.ChildElements.ToList())
            {
                if (child is RunStyle || child is RunPropertiesChange || child is Vanish || child is WebHidden || child is SpecVanish)
                    continue;

                var clone = child.CloneNode(true);
                foreach (var old in target.ChildElements.Where(e => e.GetType() == clone.GetType()).ToList())
                    old.Remove();

                target.AddChild(clone);
            }
        }

        private static void Flag<T>(OpenXmlCompositeElement properties, bool value, WordInheritedRun inherited) where T : OnOffType, new()
        {
            properties.RemoveAllChildren<T>();
            if (value)
                properties.AddChild(new T());
            else if (inherited.IsOn<T>())
                properties.AddChild(new T { Val = false });
        }

        private static void SetHighlight(OpenXmlCompositeElement properties, string value, WordInheritedRun inherited)
        {
            if (properties is not StyleRunProperties)
                properties.RemoveAllChildren<Highlight>();

            properties.RemoveAllChildren<Shading>();
            if (value == "none")
            {
                if (inherited.Element<Highlight>() != null && properties is not StyleRunProperties)
                    properties.AddChild(new Highlight { Val = HighlightColorValues.None });

                if (inherited.Element<Shading>() != null)
                    properties.AddChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "auto" });

                return;
            }

            var named = Named(value);
            if (named != null && properties is not StyleRunProperties)
                properties.AddChild(new Highlight { Val = named.Value });
            else
                properties.AddChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = Fill(value) });
        }

        private static string Fill(string value) => value.ToLowerInvariant() switch
        {
            "yellow" => "FFFF00",
            "green" => "00FF00",
            "cyan" => "00FFFF",
            "magenta" => "FF00FF",
            "blue" => "0000FF",
            "red" => "FF0000",
            "darkblue" => "000080",
            "darkcyan" => "008080",
            "darkgreen" => "008000",
            "darkmagenta" => "800080",
            "darkred" => "800000",
            "darkyellow" => "808000",
            "darkgray" => "808080",
            "lightgray" => "C0C0C0",
            "black" => "000000",
            "white" => "FFFFFF",
            _ => value,
        };

        private static string ParseHighlight(string value)
        {
            var text = value.Trim();
            if (text.Equals("none", StringComparison.OrdinalIgnoreCase))
                return "none";

            var named = WordPresets.HighlightNames.FirstOrDefault(n => n.Equals(text, StringComparison.OrdinalIgnoreCase));
            return named ?? Hex(text, "highlight");
        }

        private static HighlightColorValues? Named(string value)
        {
            if (value.Length == 6 && value.All(Uri.IsHexDigit))
                return null;

            return value.ToLowerInvariant() switch
            {
                "yellow" => HighlightColorValues.Yellow,
                "green" => HighlightColorValues.Green,
                "cyan" => HighlightColorValues.Cyan,
                "magenta" => HighlightColorValues.Magenta,
                "blue" => HighlightColorValues.Blue,
                "red" => HighlightColorValues.Red,
                "darkblue" => HighlightColorValues.DarkBlue,
                "darkcyan" => HighlightColorValues.DarkCyan,
                "darkgreen" => HighlightColorValues.DarkGreen,
                "darkmagenta" => HighlightColorValues.DarkMagenta,
                "darkred" => HighlightColorValues.DarkRed,
                "darkyellow" => HighlightColorValues.DarkYellow,
                "darkgray" => HighlightColorValues.DarkGray,
                "lightgray" => HighlightColorValues.LightGray,
                "black" => HighlightColorValues.Black,
                "white" => HighlightColorValues.White,
                _ => null,
            };
        }

        private static UnderlineValues? ParseUnderline(string value)
        {
            if (value == null)
                return null;

            switch (value.Trim().ToLowerInvariant())
            {
                case "none":
                    return UnderlineValues.None;
                case "single":
                    return UnderlineValues.Single;
                case "double":
                    return UnderlineValues.Double;
                case "thick":
                    return UnderlineValues.Thick;
                case "dotted":
                    return UnderlineValues.Dotted;
                case "dashed":
                    return UnderlineValues.Dash;
                case "wave":
                    return UnderlineValues.Wave;
                case "words":
                    return UnderlineValues.Words;
                default:
                    throw new ToolInputException("'underline' 값은 none, single, double, thick, dotted, dashed, wave, words 중 하나여야 해요.");
            }
        }

        private static string Pick(string value, string label, params string[] allowed)
        {
            if (value == null)
                return null;

            var text = value.Trim().ToLowerInvariant();
            if (!allowed.Contains(text))
                throw new ToolInputException($"'{label}' 값은 {string.Join(", ", allowed)} 중 하나여야 해요.");

            return text;
        }

        private static string Hex(string value, string label)
        {
            var text = (value ?? string.Empty).Trim().TrimStart('#');
            if (text.Length != 6 || !text.All(Uri.IsHexDigit))
                throw new ToolInputException($"'{label}' 값은 #RRGGBB 형식의 색이어야 해요. 예: #FF0000");

            return text.ToUpperInvariant();
        }
    }
}
