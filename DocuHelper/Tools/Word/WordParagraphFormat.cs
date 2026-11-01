using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordParagraphFormat
    {
        public const int MaxStyleProperties = 20;

        public static readonly IReadOnlyList<string> StyleKeys = new[]
        {
            "align", "indent_left_cm", "indent_right_cm", "first_line_cm", "hanging_cm", "space_before_pt", "space_after_pt",
            "line_spacing", "keep_with_next", "keep_lines", "page_break_before", "widow_control", "outline_level", "shading",
            "borders", "suppress_line_numbers",
        };

        private static readonly string[] s_sides = { "top", "left", "bottom", "right" };

        public static WordParagraphSpec Parse(ToolInput input, bool forStyle = false)
        {
            var spec = new WordParagraphSpec();
            spec.Reset = !forStyle && input.GetOptionalBool("reset") == true;
            spec.Align = ParseAlign(input.GetOptionalString("align"));

            spec.Left = Length(input, "indent_left_cm", -10, 30);
            spec.Right = Length(input, "indent_right_cm", -10, 30);
            spec.FirstLine = Length(input, "first_line_cm", 0, 30);
            spec.Hanging = Length(input, "hanging_cm", 0, 30);
            if (spec.FirstLine != null && spec.Hanging != null)
                throw new ToolInputException("first_line_cm와 hanging_cm는 함께 쓸 수 없어요. 첫 줄만 들여쓰려면 first_line_cm, 둘째 줄부터 들여쓰려면 hanging_cm 하나만 넣어 주세요.");

            if (!forStyle)
            {
                spec.IndentStep = input.GetOptionalInt("indent_step");
                if (spec.IndentStep != null && spec.IndentStep != 1 && spec.IndentStep != -1)
                    throw new ToolInputException("'indent_step' 값은 -1 또는 1이어야 해요.");

                if (spec.IndentStep != null && spec.Left != null)
                    throw new ToolInputException("indent_step과 indent_left_cm는 함께 쓸 수 없어요. 하나만 넣어 주세요.");
            }

            spec.Before = Points(input, "space_before_pt");
            spec.After = Points(input, "space_after_pt");
            ParseLine(input, spec);

            spec.KeepNext = input.GetOptionalBool("keep_with_next");
            spec.KeepLines = input.GetOptionalBool("keep_lines");
            spec.PageBreakBefore = input.GetOptionalBool("page_break_before");
            spec.WidowControl = input.GetOptionalBool("widow_control");
            spec.SuppressLineNumbers = input.GetOptionalBool("suppress_line_numbers");

            var outline = input.GetOptionalInt("outline_level");
            if (outline != null)
            {
                if (outline < 0 || outline > 9)
                    throw new ToolInputException("'outline_level' 값은 0~9 사이여야 해요. 0은 본문(목차에 안 나옴), 1~9는 제목 수준이에요.");

                spec.OutlineLevel = outline;
            }

            spec.Shading = ParseShading(input.GetOptionalString("shading"));
            spec.Borders = ParseBorders(input);
            return spec;
        }

        public static WordParagraphSpec ParseObject(ToolInput input, string label)
        {
            if (input.Root.ValueKind != JsonValueKind.Object)
                throw new ToolInputException($"'{label}' 값은 속성 목록이어야 해요.");

            if (input.Root.EnumerateObject().Count() > MaxStyleProperties)
                throw new ToolInputException($"'{label}'에는 속성을 {MaxStyleProperties}개까지만 넣을 수 있어요.");

            foreach (var property in input.Root.EnumerateObject())
            {
                if (!StyleKeys.Contains(property.Name))
                    throw new ToolInputException($"'{label}'에 ‘{property.Name}’은(는) 쓸 수 없어요. 쓸 수 있는 값: {string.Join(", ", StyleKeys)}");
            }

            return Parse(input, true);
        }

        public static void Apply(OpenXmlCompositeElement properties, WordParagraphSpec spec, WordInheritedParagraph inherited, bool dedupe, int defaultTab = 720)
        {
            if (spec.Reset)
                Reset(properties);

            if (spec.Align != null)
                SetAlign(properties, spec.Align.Value, inherited, dedupe);

            if (spec.HasIndent)
                SetIndent(properties, spec, inherited, dedupe, defaultTab);

            if (spec.Before != null || spec.After != null || spec.Line != null)
                SetSpacing(properties, spec, inherited, dedupe);

            if (spec.KeepNext != null)
                SetFlag<KeepNext>(properties, spec.KeepNext.Value, inherited.IsOn<KeepNext>(), dedupe);

            if (spec.KeepLines != null)
                SetFlag<KeepLines>(properties, spec.KeepLines.Value, inherited.IsOn<KeepLines>(), dedupe);

            if (spec.PageBreakBefore != null)
                SetFlag<PageBreakBefore>(properties, spec.PageBreakBefore.Value, inherited.IsOn<PageBreakBefore>(), dedupe);

            if (spec.WidowControl != null)
                SetFlag<WidowControl>(properties, spec.WidowControl.Value, inherited.IsOn<WidowControl>(true), dedupe);

            if (spec.SuppressLineNumbers != null)
                SetFlag<SuppressLineNumbers>(properties, spec.SuppressLineNumbers.Value, inherited.IsOn<SuppressLineNumbers>(), dedupe);

            if (spec.OutlineLevel != null)
                SetOutline(properties, spec.OutlineLevel.Value, inherited);

            if (spec.Shading != null)
                SetShading(properties, spec.Shading, inherited, dedupe);

            if (spec.Borders != null)
                SetBorders(properties, spec.Borders, inherited);
        }

        public static List<(string English, string Korean)> Describe(WordParagraphSpec spec)
        {
            var parts = new List<(string English, string Korean)>();
            if (spec.Reset)
                parts.Add(("direct paragraph formatting cleared", "문단 서식 지움"));

            if (spec.Align != null)
            {
                var value = spec.Align.Value;
                if (value == JustificationValues.Center)
                    parts.Add(("centered", "가운데"));
                else if (value == JustificationValues.Right)
                    parts.Add(("right aligned", "오른쪽"));
                else if (value == JustificationValues.Both)
                    parts.Add(("justified", "양쪽 맞춤"));
                else if (value == JustificationValues.Distribute)
                    parts.Add(("distributed", "나눔 맞춤"));
                else if (value == JustificationValues.ThaiDistribute)
                    parts.Add(("divided", "나눔 정렬"));
                else
                    parts.Add(("left aligned", "왼쪽"));
            }

            if (spec.IndentStep != null)
                parts.Add((spec.IndentStep > 0 ? "indent increased" : "indent decreased", spec.IndentStep > 0 ? "들여쓰기" : "내어쓰기"));

            if (spec.Left != null)
                parts.Add(($"left indent {Cm(spec.Left.Value)}cm", $"왼쪽 들여쓰기 {Cm(spec.Left.Value)}cm"));

            if (spec.Right != null)
                parts.Add(($"right indent {Cm(spec.Right.Value)}cm", $"오른쪽 들여쓰기 {Cm(spec.Right.Value)}cm"));

            if (spec.FirstLine != null)
                parts.Add(($"first line indent {Cm(spec.FirstLine.Value)}cm", $"첫 줄 들여쓰기 {Cm(spec.FirstLine.Value)}cm"));

            if (spec.Hanging != null)
                parts.Add(($"hanging indent {Cm(spec.Hanging.Value)}cm", $"내어쓰기 {Cm(spec.Hanging.Value)}cm"));

            if (spec.Before != null)
                parts.Add(($"space before {Pt(spec.Before.Value)}pt", $"앞 간격 {Pt(spec.Before.Value)}pt"));

            if (spec.After != null)
                parts.Add(($"space after {Pt(spec.After.Value)}pt", $"뒤 간격 {Pt(spec.After.Value)}pt"));

            if (spec.Line != null)
            {
                if (spec.LineRule == LineSpacingRuleValues.Exact)
                    parts.Add(($"exact line spacing {Pt(spec.Line.Value)}pt", $"줄 간격 {Pt(spec.Line.Value)}pt 고정"));
                else if (spec.LineRule == LineSpacingRuleValues.AtLeast)
                    parts.Add(($"line spacing at least {Pt(spec.Line.Value)}pt", $"줄 간격 최소 {Pt(spec.Line.Value)}pt"));
                else
                    parts.Add(($"line spacing {Lines(spec.Line.Value)}", $"줄 간격 {Lines(spec.Line.Value)}배"));
            }

            Flag(parts, spec.KeepNext, "keep with next", "다음 문단과 함께", "keep with next off", "다음 문단과 함께 끔");
            Flag(parts, spec.KeepLines, "keep lines together", "문단 나누지 않음", "keep lines together off", "문단 나눔 허용");
            Flag(parts, spec.PageBreakBefore, "page break before", "앞에서 쪽 나눔", "page break before off", "앞 쪽 나눔 끔");
            Flag(parts, spec.WidowControl, "widow control", "외톨이 줄 방지", "widow control off", "외톨이 줄 방지 끔");
            Flag(parts, spec.SuppressLineNumbers, "line numbers suppressed", "줄 번호 제외", "line numbers allowed", "줄 번호 포함");

            if (spec.OutlineLevel != null)
            {
                parts.Add(spec.OutlineLevel == 0
                    ? ("outline level body text", "개요 수준 본문")
                    : ($"outline level {spec.OutlineLevel}", $"개요 수준 {spec.OutlineLevel}"));
            }

            if (spec.Shading != null)
                parts.Add(spec.Shading == "none" ? ("shading removed", "음영 없음") : ($"shading #{spec.Shading}", "음영"));

            if (spec.Borders != null)
                parts.Add(spec.Borders.None ? ("borders removed", "테두리 없음") : ("borders", "테두리"));

            return parts;
        }

        public static string Icon(WordParagraphSpec spec)
        {
            if (spec.Align == JustificationValues.Center)
                return "align-center";

            if (spec.Align == JustificationValues.Left)
                return "align-left";

            return spec.HasIndent ? "indent" : "pencil";
        }

        public static JustificationValues? ParseAlign(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "":
                    return null;
                case "left":
                case "start":
                    return JustificationValues.Left;
                case "center":
                case "centre":
                case "middle":
                    return JustificationValues.Center;
                case "right":
                case "end":
                    return JustificationValues.Right;
                case "justify":
                case "justified":
                case "both":
                    return JustificationValues.Both;
                case "distribute":
                case "distributed":
                    return JustificationValues.Distribute;
                case "divide":
                    return JustificationValues.ThaiDistribute;
                default:
                    throw new ToolInputException("'align' 값은 left, center, right, justify, distribute, divide 중 하나여야 해요.");
            }
        }

        public static string AlignName(JustificationValues? value)
        {
            if (value == null || value == JustificationValues.Left || value == JustificationValues.Start)
                return "left";

            if (value == JustificationValues.Center)
                return "center";

            if (value == JustificationValues.Right || value == JustificationValues.End)
                return "right";

            if (value == JustificationValues.ThaiDistribute)
                return "divide";

            return value == JustificationValues.Distribute ? "distribute" : "justify";
        }

        public static void Reset(OpenXmlCompositeElement properties)
        {
            foreach (var child in properties.ChildElements.ToList())
            {
                if (child is ParagraphStyleId || child is NumberingProperties || child is SectionProperties || child is ParagraphMarkRunProperties || child is ParagraphPropertiesChange)
                    continue;

                child.Remove();
            }
        }

        private static void SetAlign(OpenXmlCompositeElement properties, JustificationValues value, WordInheritedParagraph inherited, bool dedupe)
        {
            properties.RemoveAllChildren<Justification>();
            var current = inherited.Element<Justification>()?.Val?.Value;
            if (dedupe && AlignName(current) == AlignName(value))
                return;

            properties.AddChild(new Justification { Val = value });
        }

        private static void SetIndent(OpenXmlCompositeElement properties, WordParagraphSpec spec, WordInheritedParagraph inherited, bool dedupe, int defaultTab)
        {
            var indent = properties.GetFirstChild<Indentation>();
            var created = indent == null;
            indent ??= new Indentation();

            var left = spec.Left;
            if (spec.IndentStep != null)
            {
                var current = WordInheritedParagraph.Parse(indent.Left?.Value ?? indent.Start?.Value) ?? inherited.Indent(i => i.Left?.Value ?? i.Start?.Value);
                left = Step(current, spec.IndentStep.Value, defaultTab);
            }

            if (left != null)
            {
                indent.Left = null;
                indent.Start = null;
                indent.LeftChars = null;
                indent.StartCharacters = null;
                if (!(dedupe && left.Value == inherited.Indent(i => i.Left?.Value ?? i.Start?.Value)))
                    indent.Left = Text(left.Value);
            }

            if (spec.Right != null)
            {
                indent.Right = null;
                indent.End = null;
                indent.RightChars = null;
                indent.EndCharacters = null;
                if (!(dedupe && spec.Right.Value == inherited.Indent(i => i.Right?.Value ?? i.End?.Value)))
                    indent.Right = Text(spec.Right.Value);
            }

            if (spec.FirstLine != null || spec.Hanging != null)
            {
                var wanted = spec.Hanging != null ? -spec.Hanging.Value : spec.FirstLine.Value;
                var hanging = inherited.Indent(i => i.Hanging?.Value);
                var present = hanging > 0 ? -hanging : inherited.Indent(i => i.FirstLine?.Value);
                indent.FirstLine = null;
                indent.Hanging = null;
                indent.FirstLineChars = null;
                indent.HangingChars = null;
                if (wanted > 0 && !(dedupe && wanted == present))
                    indent.FirstLine = Text(wanted);
                else if (wanted < 0 && !(dedupe && wanted == present))
                    indent.Hanging = Text(-wanted);
                else if (wanted == 0 && present != 0)
                    indent.FirstLine = "0";
            }

            if (!indent.GetAttributes().Any())
            {
                if (!created)
                    indent.Remove();

                return;
            }

            if (created)
                properties.AddChild(indent);
        }

        private static int Step(int current, int step, int tab)
        {
            var size = Math.Max(1, tab);
            var next = step > 0 ? (current / size + 1) * size : ((current + size - 1) / size - 1) * size;
            return Math.Clamp(next, 0, WordUnits.CmToTwips(30));
        }

        private static void SetSpacing(OpenXmlCompositeElement properties, WordParagraphSpec spec, WordInheritedParagraph inherited, bool dedupe)
        {
            var spacing = properties.GetFirstChild<SpacingBetweenLines>();
            var created = spacing == null;
            spacing ??= new SpacingBetweenLines();

            if (spec.Before != null)
            {
                spacing.Before = null;
                spacing.BeforeLines = null;
                spacing.BeforeAutoSpacing = null;
                var present = WordInheritedParagraph.Parse(inherited.Attribute<SpacingBetweenLines, string>(s => s.Before?.Value)) ?? 0;
                if (!(dedupe && spec.Before.Value == present))
                    spacing.Before = Text(spec.Before.Value);
            }

            if (spec.After != null)
            {
                spacing.After = null;
                spacing.AfterLines = null;
                spacing.AfterAutoSpacing = null;
                var present = WordInheritedParagraph.Parse(inherited.Attribute<SpacingBetweenLines, string>(s => s.After?.Value)) ?? 0;
                if (!(dedupe && spec.After.Value == present))
                    spacing.After = Text(spec.After.Value);
            }

            if (spec.Line != null)
            {
                spacing.Line = null;
                spacing.LineRule = null;
                var line = WordInheritedParagraph.Parse(inherited.Attribute<SpacingBetweenLines, string>(s => s.Line?.Value)) ?? 240;
                var rule = inherited.Attribute<SpacingBetweenLines, LineSpacingRuleValues?>(s => s.LineRule?.Value) ?? LineSpacingRuleValues.Auto;
                if (!(dedupe && spec.Line.Value == line && spec.LineRule == rule))
                {
                    spacing.Line = Text(spec.Line.Value);
                    spacing.LineRule = spec.LineRule;
                }
            }

            if (!spacing.GetAttributes().Any())
            {
                if (!created)
                    spacing.Remove();

                return;
            }

            if (created)
                properties.AddChild(spacing);
        }

        private static void SetFlag<T>(OpenXmlCompositeElement properties, bool value, bool inherited, bool dedupe) where T : OnOffType, new()
        {
            properties.RemoveAllChildren<T>();
            if (value)
            {
                if (!(dedupe && inherited))
                    properties.AddChild(new T());

                return;
            }

            if (inherited)
                properties.AddChild(new T { Val = false });
        }

        private static void SetOutline(OpenXmlCompositeElement properties, int level, WordInheritedParagraph inherited)
        {
            properties.RemoveAllChildren<OutlineLevel>();
            var wanted = level == 0 ? 9 : level - 1;
            var present = inherited.Element<OutlineLevel>()?.Val?.Value ?? 9;
            if (wanted != present)
                properties.AddChild(new OutlineLevel { Val = wanted });
        }

        private static void SetShading(OpenXmlCompositeElement properties, string fill, WordInheritedParagraph inherited, bool dedupe)
        {
            properties.RemoveAllChildren<Shading>();
            var present = inherited.Element<Shading>()?.Fill?.Value;
            var inheritedFill = string.IsNullOrEmpty(present) || present == "auto" ? "none" : present.ToUpperInvariant();
            if (dedupe && inheritedFill == fill)
                return;

            if (fill == "none")
            {
                if (inheritedFill != "none")
                    properties.AddChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "auto" });

                return;
            }

            properties.AddChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = fill });
        }

        private static void SetBorders(OpenXmlCompositeElement properties, WordParagraphBorderSpec spec, WordInheritedParagraph inherited)
        {
            if (spec.None)
            {
                properties.RemoveAllChildren<ParagraphBorders>();
                var present = inherited.Element<ParagraphBorders>();
                if (present != null && present.ChildElements.OfType<BorderType>().Any(b => b.Val?.Value != BorderValues.Nil && b.Val?.Value != BorderValues.None))
                {
                    var nil = new ParagraphBorders();
                    nil.AddChild(new TopBorder { Val = BorderValues.Nil });
                    nil.AddChild(new LeftBorder { Val = BorderValues.Nil });
                    nil.AddChild(new BottomBorder { Val = BorderValues.Nil });
                    nil.AddChild(new RightBorder { Val = BorderValues.Nil });
                    properties.AddChild(nil);
                }

                return;
            }

            var borders = properties.GetFirstChild<ParagraphBorders>();
            var created = borders == null;
            borders ??= new ParagraphBorders();
            var sides = new List<string>();
            foreach (var side in spec.Sides)
            {
                if (side == "box")
                    sides.AddRange(s_sides);
                else
                    sides.Add(side);
            }

            foreach (var side in s_sides.Where(sides.Contains))
            {
                BorderType border = side switch
                {
                    "top" => borders.GetFirstChild<TopBorder>(),
                    "left" => borders.GetFirstChild<LeftBorder>(),
                    "bottom" => borders.GetFirstChild<BottomBorder>(),
                    _ => borders.GetFirstChild<RightBorder>(),
                };

                var fresh = border == null || border.Val?.Value == BorderValues.Nil || border.Val?.Value == BorderValues.None;
                if (border == null)
                {
                    border = side switch
                    {
                        "top" => new TopBorder(),
                        "left" => new LeftBorder(),
                        "bottom" => new BottomBorder(),
                        _ => new RightBorder(),
                    };

                    borders.AddChild(border);
                }

                if (spec.Style != null || fresh)
                    border.Val = spec.Style ?? BorderValues.Single;

                if (spec.Size != null || fresh || border.Size == null)
                    border.Size = (uint)(spec.Size ?? 4);

                if (spec.Color != null || fresh || border.Color == null)
                    border.Color = spec.Color ?? "auto";

                if (spec.Space != null || fresh || border.Space == null)
                    border.Space = (uint)(spec.Space ?? (side == "left" || side == "right" ? 4 : 1));
            }

            if (created)
                properties.AddChild(borders);
        }

        private static void ParseLine(ToolInput input, WordParagraphSpec spec)
        {
            if (!input.Has("line_spacing"))
                return;

            var element = input.Get("line_spacing");
            string mode;
            double value;
            if (element.ValueKind == JsonValueKind.Number)
            {
                mode = "multiple";
                value = element.GetDouble();
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                var nested = new ToolInput(element);
                mode = (nested.GetOptionalString("mode") ?? string.Empty).Trim().ToLowerInvariant();
                if (mode.Length == 0)
                    throw new ToolInputException("'line_spacing'에 mode가 필요해요. multiple, exact, at_least 중 하나를 넣어 주세요.");

                value = nested.GetOptionalNumber("value") ?? throw new ToolInputException("'line_spacing'에 value가 필요해요. multiple은 1.0, 1.5처럼 배수, exact와 at_least는 pt예요.");
            }
            else
            {
                throw new ToolInputException("'line_spacing' 값은 {\"mode\": \"multiple\", \"value\": 1.5} 형식이어야 해요.");
            }

            switch (mode)
            {
                case "multiple":
                    WordUnits.Check(value, 0.06, 9.99, "line_spacing.value", "배");
                    spec.Line = WordUnits.LinesToTwentyFourths(value);
                    spec.LineRule = LineSpacingRuleValues.Auto;
                    break;
                case "exact":
                case "at_least":
                case "atleast":
                    WordUnits.Check(value, 0.7, 1584, "line_spacing.value", "pt");
                    spec.Line = WordUnits.PtToTwips(value);
                    spec.LineRule = mode == "exact" ? LineSpacingRuleValues.Exact : LineSpacingRuleValues.AtLeast;
                    break;
                default:
                    throw new ToolInputException("'line_spacing'의 mode는 multiple, exact, at_least 중 하나여야 해요.");
            }
        }

        private static string ParseShading(string value)
        {
            if (value == null)
                return null;

            var text = value.Trim();
            if (text.Equals("none", StringComparison.OrdinalIgnoreCase) || text.Equals("auto", StringComparison.OrdinalIgnoreCase))
                return "none";

            return Hex(text, "shading");
        }

        private static WordParagraphBorderSpec ParseBorders(ToolInput input)
        {
            if (!input.Has("borders"))
                return null;

            var element = input.Get("borders");
            if (element.ValueKind != JsonValueKind.Object)
                throw new ToolInputException("'borders' 값은 {\"sides\": [\"box\"], \"style\": \"single\"} 형식이어야 해요.");

            var nested = new ToolInput(element);
            var spec = new WordParagraphBorderSpec();
            var sides = nested.Has("sides") ? nested.GetStringList("sides").Select(s => (s ?? string.Empty).Trim().ToLowerInvariant()).Distinct().ToList() : new List<string> { "box" };
            if (sides.Count == 0)
                sides.Add("box");

            foreach (var side in sides)
            {
                if (side != "top" && side != "bottom" && side != "left" && side != "right" && side != "box" && side != "none")
                    throw new ToolInputException("'borders.sides'에는 top, bottom, left, right, box, none만 쓸 수 있어요.");
            }

            if (sides.Contains("none"))
            {
                if (sides.Count > 1)
                    throw new ToolInputException("'borders.sides'의 none은 다른 값과 함께 쓸 수 없어요.");

                spec.None = true;
                spec.Sides = sides;
                return spec;
            }

            spec.Sides = sides;
            var style = nested.GetOptionalString("style");
            if (style != null)
            {
                spec.Style = style.Trim().ToLowerInvariant() switch
                {
                    "single" => BorderValues.Single,
                    "double" => BorderValues.Double,
                    "dotted" => BorderValues.Dotted,
                    "dashed" => BorderValues.Dashed,
                    "thick" => BorderValues.Thick,
                    _ => throw new ToolInputException("'borders.style' 값은 single, double, dotted, dashed, thick 중 하나여야 해요."),
                };
            }

            var width = nested.GetOptionalNumber("width_pt");
            if (width != null)
            {
                WordUnits.Check(width.Value, 0.25, 6, "borders.width_pt", "pt");
                spec.Size = WordUnits.PtToEighths(width.Value);
            }

            var color = nested.GetOptionalString("color");
            if (color != null)
                spec.Color = color.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase) ? "auto" : Hex(color, "borders.color");

            var space = nested.GetOptionalNumber("space_pt");
            if (space != null)
                spec.Space = (int)Math.Round(WordUnits.Check(space.Value, 0, 31, "borders.space_pt", "pt"));

            return spec;
        }

        private static string Hex(string value, string label)
        {
            var text = (value ?? string.Empty).Trim().TrimStart('#');
            if (text.Length != 6 || !text.All(Uri.IsHexDigit))
                throw new ToolInputException($"'{label}' 값은 #RRGGBB 형식의 색이어야 해요. 예: #FFF2CC");

            return text.ToUpperInvariant();
        }

        private static int? Length(ToolInput input, string name, double min, double max)
        {
            var value = input.GetOptionalNumber(name);
            return value == null ? null : WordUnits.CmToTwips(WordUnits.Check(value.Value, min, max, name, "cm"));
        }

        private static int? Points(ToolInput input, string name)
        {
            var value = input.GetOptionalNumber(name);
            return value == null ? null : WordUnits.PtToTwips(WordUnits.Check(value.Value, 0, 1584, name, "pt"));
        }

        private static void Flag(List<(string English, string Korean)> parts, bool? value, string on, string koreanOn, string off, string koreanOff)
        {
            if (value != null)
                parts.Add(value.Value ? (on, koreanOn) : (off, koreanOff));
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Cm(int twips) => WordUnits.TwipsToCm(twips).ToString("0.##", CultureInfo.InvariantCulture);

        private static string Pt(int twips) => WordUnits.TwipsToPt(twips).ToString("0.##", CultureInfo.InvariantCulture);

        private static string Lines(int value) => (value / 240d).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
