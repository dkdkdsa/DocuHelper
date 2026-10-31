using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class WordRunFormat
    {
        public const string PropertiesSchema = """
            "font": {"type": "string", "description": "Font family, e.g. \"맑은 고딕\", \"바탕\", \"Arial\". Applied to Latin and Korean text."},
            "size": {"type": "number", "minimum": 1, "maximum": 400, "description": "Font size in points; half points allowed (10.5)."},
            "bold": {"type": "boolean"},
            "italic": {"type": "boolean"},
            "underline": {"type": "string", "enum": ["none", "single", "double", "thick", "dotted", "dashed", "wave", "words"]},
            "strike": {"type": "string", "enum": ["none", "single", "double"]},
            "color": {"type": "string", "description": "Text color #RRGGBB, or \"auto\" for the default color."},
            "highlight": {"type": "string", "description": "Marker color: yellow, green, cyan, magenta, blue, red, darkBlue, darkCyan, darkGreen, darkMagenta, darkRed, darkYellow, darkGray, lightGray, black, white, a #RRGGBB value, or \"none\"."},
            "vertical_align": {"type": "string", "enum": ["baseline", "superscript", "subscript"]},
            "caps": {"type": "string", "enum": ["none", "all", "small"]},
            "spacing_pt": {"type": "number", "minimum": -5, "maximum": 20, "description": "Extra space between characters in points; negative condenses."}
            """;

        public static readonly IReadOnlyList<double> StandardSizes = new[] { 8d, 9, 10, 10.5, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };

        private static readonly Dictionary<string, UnderlineValues> s_underlines = new(StringComparer.OrdinalIgnoreCase)
        {
            ["none"] = UnderlineValues.None,
            ["single"] = UnderlineValues.Single,
            ["double"] = UnderlineValues.Double,
            ["thick"] = UnderlineValues.Thick,
            ["dotted"] = UnderlineValues.Dotted,
            ["dashed"] = UnderlineValues.Dash,
            ["wave"] = UnderlineValues.Wave,
            ["words"] = UnderlineValues.Words,
        };

        private static readonly Dictionary<string, HighlightColorValues> s_highlights = new(StringComparer.OrdinalIgnoreCase)
        {
            ["yellow"] = HighlightColorValues.Yellow,
            ["green"] = HighlightColorValues.Green,
            ["cyan"] = HighlightColorValues.Cyan,
            ["magenta"] = HighlightColorValues.Magenta,
            ["blue"] = HighlightColorValues.Blue,
            ["red"] = HighlightColorValues.Red,
            ["darkBlue"] = HighlightColorValues.DarkBlue,
            ["darkCyan"] = HighlightColorValues.DarkCyan,
            ["darkGreen"] = HighlightColorValues.DarkGreen,
            ["darkMagenta"] = HighlightColorValues.DarkMagenta,
            ["darkRed"] = HighlightColorValues.DarkRed,
            ["darkYellow"] = HighlightColorValues.DarkYellow,
            ["darkGray"] = HighlightColorValues.DarkGray,
            ["lightGray"] = HighlightColorValues.LightGray,
            ["black"] = HighlightColorValues.Black,
            ["white"] = HighlightColorValues.White,
        };

        private static readonly HashSet<string> s_structuralNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "hyperlink",
            "followed hyperlink",
            "footnote reference",
            "endnote reference",
            "annotation reference",
            "comment reference",
            "하이퍼링크",
            "각주 참조",
            "미주 참조",
            "메모 참조",
        };

        public string Font { get; private set; }

        public double? Size { get; private set; }

        public bool? Bold { get; private set; }

        public bool? Italic { get; private set; }

        public string Underline { get; private set; }

        public string Strike { get; private set; }

        public string Color { get; private set; }

        public string Highlight { get; private set; }

        public string VerticalAlign { get; private set; }

        public string Caps { get; private set; }

        public double? SpacingPt { get; private set; }

        public string SizeStep { get; private set; }

        public WordCaseMode? Case { get; private set; }

        public bool Clear { get; private set; }

        public bool IsEmpty =>
            Font == null && Size == null && Bold == null && Italic == null && Underline == null && Strike == null && Color == null
            && Highlight == null && VerticalAlign == null && Caps == null && SpacingPt == null && SizeStep == null && Case == null && !Clear;

        public bool ChangesProperties => !IsEmpty && (Clear || Font != null || Size != null || Bold != null || Italic != null || Underline != null
            || Strike != null || Color != null || Highlight != null || VerticalAlign != null || Caps != null || SpacingPt != null || SizeStep != null);

        public static WordRunFormat Parse(ToolInput input, string label, bool extended)
        {
            var format = new WordRunFormat();
            format.Font = ParseFont(input, label);
            format.Size = ParseSize(input, label);
            format.Bold = Flag(input, "bold", label);
            format.Italic = Flag(input, "italic", label);
            format.Underline = Choice(input, "underline", label, s_underlines.Keys);
            format.Strike = Choice(input, "strike", label, new[] { "none", "single", "double" });
            format.Color = ColorValue(input, label);
            format.Highlight = HighlightValue(input, label);
            format.VerticalAlign = Choice(input, "vertical_align", label, new[] { "baseline", "superscript", "subscript" });
            format.Caps = Choice(input, "caps", label, new[] { "none", "all", "small" });
            format.SpacingPt = ParseSpacing(input, label);
            if (extended)
            {
                format.SizeStep = Choice(input, "size_step", label, new[] { "grow", "shrink" });
                var mode = Choice(input, "case", label, new[] { "upper", "lower", "sentence", "title", "toggle" });
                if (mode != null)
                    format.Case = Enum.Parse<WordCaseMode>(mode, true);

                format.Clear = Flag(input, "clear_formatting", label) == true;
                if (format.SizeStep != null && format.Size != null)
                    throw new ToolInputException($"{Prefix(label)}size와 size_step은 함께 쓸 수 없어요. 하나만 넣어 주세요.");
            }

            return format;
        }

        public static WordRunFormat Parse(JsonElement element, string label, bool extended)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new ToolInputException($"{Prefix(label)}글자 모양은 객체여야 해요. 예: {{\"bold\": true, \"size\": 14}}");

            return Parse(new ToolInput(element), label, extended);
        }

        public void Apply(RunProperties props, WordStyles styles, Paragraph paragraph)
        {
            if (Clear)
                ClearDirect(props, styles);

            if (Font != null)
                SetFont(props, styles, paragraph);

            if (Size != null || SizeStep != null)
                SetSize(props, styles, paragraph);

            if (Bold != null)
            {
                props.Bold = Switch<Bold>(Bold.Value, props, styles, paragraph);
                props.BoldComplexScript = Switch<BoldComplexScript>(Bold.Value, props, styles, paragraph);
            }

            if (Italic != null)
            {
                props.Italic = Switch<Italic>(Italic.Value, props, styles, paragraph);
                props.ItalicComplexScript = Switch<ItalicComplexScript>(Italic.Value, props, styles, paragraph);
            }

            if (Underline != null)
                SetUnderline(props, styles, paragraph);

            if (Strike != null)
            {
                props.Strike = Switch<Strike>(Strike == "single", props, styles, paragraph);
                props.DoubleStrike = Switch<DoubleStrike>(Strike == "double", props, styles, paragraph);
            }

            if (Color != null)
                SetColor(props, styles, paragraph);

            if (Highlight != null)
                SetHighlight(props, styles, paragraph);

            if (VerticalAlign != null)
                SetVerticalAlign(props, styles, paragraph);

            if (Caps != null)
            {
                props.Caps = Switch<Caps>(Caps == "all", props, styles, paragraph);
                props.SmallCaps = Switch<SmallCaps>(Caps == "small", props, styles, paragraph);
            }

            if (SpacingPt != null)
                SetSpacing(props, styles, paragraph);
        }

        public List<(string English, string Korean)> Describe()
        {
            var parts = new List<(string English, string Korean)>();
            if (Clear)
                parts.Add(("formatting cleared", "서식 지움"));

            if (Font != null)
                parts.Add(($"font {Font}", Font));

            if (Size != null)
                parts.Add(($"size {Number(Size.Value)}", $"{Number(Size.Value)}pt"));

            if (SizeStep != null)
                parts.Add(SizeStep == "grow" ? ("size larger", "글자 크게") : ("size smaller", "글자 작게"));

            if (Bold != null)
                parts.Add(Bold.Value ? ("bold on", "굵게") : ("bold off", "굵게 끔"));

            if (Italic != null)
                parts.Add(Italic.Value ? ("italic on", "기울임") : ("italic off", "기울임 끔"));

            if (Underline != null)
                parts.Add(Underline == "none" ? ("underline off", "밑줄 끔") : ($"underline {Underline}", "밑줄"));

            if (Strike != null)
                parts.Add(Strike == "none" ? ("strikethrough off", "취소선 끔") : ($"strikethrough {Strike}", "취소선"));

            if (Color != null)
                parts.Add(Color == "auto" ? ("color auto", "글자색 자동") : ($"color #{Color}", $"글자색 #{Color}"));

            if (Highlight != null)
                parts.Add(Highlight == "none" ? ("highlight off", "형광펜 끔") : ($"highlight {Highlight}", "형광펜"));

            if (VerticalAlign != null)
            {
                parts.Add(VerticalAlign switch
                {
                    "superscript" => ("superscript", "위 첨자"),
                    "subscript" => ("subscript", "아래 첨자"),
                    _ => ("baseline", "첨자 끔"),
                });
            }

            if (Caps != null)
            {
                parts.Add(Caps switch
                {
                    "all" => ("all caps", "모두 대문자"),
                    "small" => ("small caps", "작은 대문자"),
                    _ => ("caps off", "대문자 효과 끔"),
                });
            }

            if (SpacingPt != null)
                parts.Add(($"spacing {Number(SpacingPt.Value)}pt", $"자간 {Number(SpacingPt.Value)}pt"));

            if (Case != null)
            {
                parts.Add(Case.Value switch
                {
                    WordCaseMode.Upper => ("upper case", "대문자로"),
                    WordCaseMode.Lower => ("lower case", "소문자로"),
                    WordCaseMode.Sentence => ("sentence case", "문장 첫 글자 대문자"),
                    WordCaseMode.Title => ("title case", "단어 첫 글자 대문자"),
                    _ => ("toggle case", "대소문자 바꿈"),
                });
            }

            return parts;
        }

        public string Icon()
        {
            if (Bold != null)
                return "bold";

            if (Italic != null)
                return "italic";

            if (Underline != null)
                return "underline";

            if (Color != null)
                return "text-color";

            if (Highlight != null)
                return "highlighter";

            return "type";
        }

        public static ParagraphMarkRunProperties ApplyToMark(ParagraphMarkRunProperties mark, WordRunFormat format, WordStyles styles, Paragraph paragraph)
        {
            var revisions = mark?.ChildElements
                .Where(c => c is Inserted || c is Deleted || c is MoveFrom || c is MoveTo)
                .Select(c => c.CloneNode(true))
                .ToList() ?? new List<OpenXmlElement>();
            var changes = mark?.ChildElements.Where(c => c is ParagraphMarkRunPropertiesChange).Select(c => c.CloneNode(true)).ToList() ?? new List<OpenXmlElement>();
            var props = mark == null ? new RunProperties() : WordText.FromMark(mark) ?? new RunProperties();
            format.Apply(props, styles, paragraph);
            var result = props.HasChildren ? WordText.ToMark(props) : new ParagraphMarkRunProperties();
            for (var i = 0; i < revisions.Count; i++)
                result.InsertAt(revisions[i], i);

            foreach (var change in changes)
                result.AppendChild(change);

            return result.HasChildren ? result : null;
        }

        public static double NextSize(double points, bool grow)
        {
            if (grow)
            {
                foreach (var size in StandardSizes)
                {
                    if (size > points + 0.001)
                        return size;
                }

                return points;
            }

            for (var i = StandardSizes.Count - 1; i >= 0; i--)
            {
                if (StandardSizes[i] < points - 0.001)
                    return StandardSizes[i];
            }

            return points;
        }

        private static string Prefix(string label) => string.IsNullOrEmpty(label) ? string.Empty : label + ": ";

        private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string ParseFont(ToolInput input, string label)
        {
            if (!input.Has("font"))
                return null;

            var value = (input.GetOptionalString("font") ?? string.Empty).Trim();
            if (value.Length == 0 || value.Length > 64 || value.Any(char.IsControl))
                throw new ToolInputException($"{Prefix(label)}font는 글꼴 이름이어야 해요. 예: \"맑은 고딕\"");

            return value;
        }

        private static double? ParseSize(ToolInput input, string label)
        {
            var value = ReadNumber(input, "size", label);
            if (value == null)
                return null;

            if (value.Value < 1 || value.Value > 400)
                throw new ToolInputException($"{Prefix(label)}size는 1~400 사이여야 해요.");

            if (Math.Abs(value.Value * 2 - Math.Round(value.Value * 2)) > 1e-6)
                throw new ToolInputException($"{Prefix(label)}size는 0.5pt 단위로 넣어 주세요. 예: 10, 10.5");

            return Math.Round(value.Value * 2) / 2;
        }

        private static double? ParseSpacing(ToolInput input, string label)
        {
            var value = ReadNumber(input, "spacing_pt", label);
            if (value == null)
                return null;

            if (value.Value < -5 || value.Value > 20)
                throw new ToolInputException($"{Prefix(label)}spacing_pt는 -5~20 사이여야 해요.");

            return Math.Round(value.Value * 20) / 20;
        }

        private static double? ReadNumber(ToolInput input, string name, string label)
        {
            try
            {
                var value = input.GetOptionalNumber(name);
                if (value != null && (double.IsNaN(value.Value) || double.IsInfinity(value.Value)))
                    throw new ToolInputException($"'{name}' 값은 숫자여야 해요.");

                return value;
            }
            catch (ToolInputException e)
            {
                throw new ToolInputException(Prefix(label) + e.Message);
            }
        }

        private static bool? Flag(ToolInput input, string name, string label)
        {
            try
            {
                return input.GetOptionalBool(name);
            }
            catch (ToolInputException e)
            {
                throw new ToolInputException(Prefix(label) + e.Message);
            }
        }

        private static string Choice(ToolInput input, string name, string label, IEnumerable<string> allowed)
        {
            if (!input.Has(name))
                return null;

            var value = (input.GetOptionalString(name) ?? string.Empty).Trim();
            var match = allowed.FirstOrDefault(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                throw new ToolInputException($"{Prefix(label)}{name} 값은 {string.Join(", ", allowed)} 중 하나여야 해요.");

            return match;
        }

        private static string ColorValue(ToolInput input, string label)
        {
            if (!input.Has("color"))
                return null;

            var value = (input.GetOptionalString("color") ?? string.Empty).Trim();
            if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
                return "auto";

            var hex = Hex(value);
            return hex ?? throw new ToolInputException($"{Prefix(label)}color는 #RRGGBB 형식이거나 auto여야 해요. 예: #C00000");
        }

        private static string HighlightValue(ToolInput input, string label)
        {
            if (!input.Has("highlight"))
                return null;

            var value = (input.GetOptionalString("highlight") ?? string.Empty).Trim();
            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                return "none";

            var named = s_highlights.Keys.FirstOrDefault(k => string.Equals(k, value, StringComparison.OrdinalIgnoreCase));
            if (named != null)
                return named;

            var hex = Hex(value);
            return hex == null ? throw new ToolInputException($"{Prefix(label)}highlight는 {string.Join(", ", s_highlights.Keys)}, #RRGGBB, none 중 하나여야 해요.") : "#" + hex;
        }

        private static string Hex(string value)
        {
            var digits = value.StartsWith('#') ? value.Substring(1) : value;
            if (digits.Length != 6 || !digits.All(Uri.IsHexDigit))
                return null;

            return digits.ToUpperInvariant();
        }

        private static T Inherit<T>(RunProperties props, WordStyles styles, Paragraph paragraph) where T : OpenXmlElement
        {
            foreach (var style in styles.Chain(props.RunStyle?.Val?.Value))
            {
                var value = style.StyleRunProperties?.GetFirstChild<T>();
                if (value != null)
                    return value;
            }

            return styles.StyleRunValue<T>(paragraph);
        }

        private static T Switch<T>(bool on, RunProperties props, WordStyles styles, Paragraph paragraph) where T : OnOffType, new()
        {
            var inherited = WordStyles.IsOn(Inherit<T>(props, styles, paragraph));
            if (on == inherited)
                return null;

            var flag = new T();
            if (!on)
                flag.Val = false;

            return flag;
        }

        private static void ClearDirect(RunProperties props, WordStyles styles)
        {
            var styleId = props.RunStyle?.Val?.Value;
            var keep = styleId != null && IsStructural(styles, styleId) ? (RunStyle)props.RunStyle.CloneNode(true) : null;
            foreach (var child in props.ChildElements.Where(c => c is not RunPropertiesChange).ToList())
                child.Remove();

            if (keep != null)
                props.RunStyle = keep;
        }

        private static bool IsStructural(WordStyles styles, string styleId)
        {
            foreach (var style in styles.Chain(styleId))
            {
                var name = style.StyleName?.Val?.Value;
                if (name != null && s_structuralNames.Contains(name.Trim()))
                    return true;
            }

            return false;
        }

        private void SetFont(RunProperties props, WordStyles styles, Paragraph paragraph)
        {
            var inherited = Inherit<RunFonts>(props, styles, paragraph);
            var same = inherited != null
                && inherited.AsciiTheme == null && inherited.HighAnsiTheme == null && inherited.EastAsiaTheme == null && inherited.ComplexScriptTheme == null
                && inherited.Ascii?.Value == Font && inherited.HighAnsi?.Value == Font && inherited.EastAsia?.Value == Font && inherited.ComplexScript?.Value == Font;
            if (same)
            {
                props.RunFonts = null;
                return;
            }

            var fonts = props.RunFonts ?? new RunFonts();
            fonts.Ascii = Font;
            fonts.HighAnsi = Font;
            fonts.EastAsia = Font;
            fonts.ComplexScript = Font;
            fonts.AsciiTheme = null;
            fonts.HighAnsiTheme = null;
            fonts.EastAsiaTheme = null;
            fonts.ComplexScriptTheme = null;
            props.RunFonts = fonts;
        }

        private void SetSize(RunProperties props, WordStyles styles, Paragraph paragraph)
        {
            var points = Size;
            if (points == null)
            {
                var current = Half(props.FontSize?.Val?.Value) ?? Half(Inherit<FontSize>(props, styles, paragraph)?.Val?.Value) ?? Half(styles.ParagraphValue<FontSize>(null)?.Val?.Value) ?? 20;
                points = NextSize(current / 2.0, SizeStep == "grow");
            }

            var half = (int)Math.Round(points.Value * 2);
            var text = half.ToString(CultureInfo.InvariantCulture);
            props.FontSize = Half(Inherit<FontSize>(props, styles, paragraph)?.Val?.Value) == half ? null : new FontSize { Val = text };
            props.FontSizeComplexScript = Half(Inherit<FontSizeComplexScript>(props, styles, paragraph)?.Val?.Value) == half ? null : new FontSizeComplexScript { Val = text };
        }

        private static int? Half(string value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0 ? number : null;

        private void SetUnderline(RunProperties props, WordStyles styles, Paragraph paragraph)
        {
            var wanted = s_underlines[Underline];
            var inherited = Inherit<Underline>(props, styles, paragraph);
            var current = inherited == null ? UnderlineValues.None : inherited.Val?.Value ?? UnderlineValues.Single;
            props.Underline = wanted == current ? null : new Underline { Val = wanted };
        }

        private void SetColor(RunProperties props, WordStyles styles, Paragraph paragraph)
        {
            var inherited = Inherit<DocumentFormat.OpenXml.Wordprocessing.Color>(props, styles, paragraph);
            var plain = inherited == null || (inherited.ThemeColor == null && inherited.ThemeShade == null && inherited.ThemeTint == null);
            if (Color == "auto")
            {
                var automatic = inherited == null || (plain && string.Equals(inherited.Val?.Value, "auto", StringComparison.OrdinalIgnoreCase));
                props.Color = automatic ? null : new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "auto" };
                return;
            }

            var same = inherited != null && plain && string.Equals(inherited.Val?.Value, Color, StringComparison.OrdinalIgnoreCase);
            props.Color = same ? null : new DocumentFormat.OpenXml.Wordprocessing.Color { Val = Color };
        }

        private void SetHighlight(RunProperties props, WordStyles styles, Paragraph paragraph)
        {
            props.Shading = null;
            if (Highlight == "none")
            {
                var inherited = Inherit<DocumentFormat.OpenXml.Wordprocessing.Highlight>(props, styles, paragraph);
                var active = inherited?.Val != null && inherited.Val.Value != HighlightColorValues.None;
                props.Highlight = active ? new DocumentFormat.OpenXml.Wordprocessing.Highlight { Val = HighlightColorValues.None } : null;
                return;
            }

            if (Highlight[0] == '#')
            {
                props.Highlight = null;
                props.Shading = new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = Highlight.Substring(1) };
                return;
            }

            props.Highlight = new DocumentFormat.OpenXml.Wordprocessing.Highlight { Val = s_highlights[Highlight] };
        }

        private void SetVerticalAlign(RunProperties props, WordStyles styles, Paragraph paragraph)
        {
            var wanted = VerticalAlign switch
            {
                "superscript" => VerticalPositionValues.Superscript,
                "subscript" => VerticalPositionValues.Subscript,
                _ => VerticalPositionValues.Baseline,
            };

            var inherited = Inherit<VerticalTextAlignment>(props, styles, paragraph)?.Val?.Value ?? VerticalPositionValues.Baseline;
            props.VerticalTextAlignment = wanted == inherited ? null : new VerticalTextAlignment { Val = wanted };
        }

        private void SetSpacing(RunProperties props, WordStyles styles, Paragraph paragraph)
        {
            var twips = (int)Math.Round(SpacingPt.Value * 20);
            var inherited = 0;
            try
            {
                inherited = Inherit<Spacing>(props, styles, paragraph)?.Val?.Value ?? 0;
            }
            catch (FormatException)
            {
                inherited = 0;
            }

            props.Spacing = twips == inherited ? null : new Spacing { Val = twips };
        }
    }
}
