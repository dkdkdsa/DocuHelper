using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public class FormatSlideParagraphsTool : Tool
    {
        public override string Name => "format_slide_paragraphs";

        public override string Description =>
            "Change paragraph layout of one shape: alignment (incl. justify), bullets and numbering, list level, line spacing, space before/after, vertical anchor, autofit. "
            + "Give only what should change. All paragraphs change unless `paragraphs` limits it. Never type bullet characters into the text; use `bullet`.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "shape": {"type": "string", "description": "Shape id from read_slides, or title, subtitle, body."},
                "paragraphs": {"type": "array", "description": "Paragraph numbers, 1-based. Default all.", "items": {"type": "integer", "minimum": 1}},
                "align": {"type": "string", "enum": ["left", "center", "right", "justify"]},
                "bullet": {"type": "string", "enum": ["none", "bullet", "number"]},
                "bullet_char": {"type": "string", "description": "One character, default •."},
                "number_style": {
                  "type": "string",
                  "enum": ["arabic_period", "arabic_paren", "lower_alpha_period", "lower_alpha_paren", "upper_alpha_period", "lower_roman_period", "upper_roman_period"],
                  "description": "Default arabic_period (1. 2. 3.)."
                },
                "start_at": {"type": "integer", "minimum": 1, "description": "First number."},
                "level": {"type": "integer", "minimum": 1, "maximum": 9, "description": "List level 1-9."},
                "level_change": {"type": "integer", "minimum": -8, "maximum": 8, "description": "+1 / -1: raise or lower the level."},
                "line_spacing": {"type": "number", "minimum": 0.5, "maximum": 9.99, "description": "Multiple of single spacing, 0.5-9.99."},
                "space_before": {"type": "number", "minimum": 0, "maximum": 999, "description": "Points."},
                "space_after": {"type": "number", "minimum": 0, "maximum": 999, "description": "Points."},
                "anchor": {"type": "string", "enum": ["top", "middle", "bottom"], "description": "Vertical position of the text in the shape."},
                "autofit": {"type": "string", "enum": ["none", "shrink", "resize_shape"]}
              },
              "required": ["slide", "shape"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var key = input.GetString("shape");
            var format = Parse(input);
            var paragraphs = PptInputs.IntegerList(input, "paragraphs", 1, 9999, 1000);

            var target = PptTextTarget.Resolve(document, slide, key);
            if (target.IsTable && (format.Anchor != null || format.Autofit != null))
                throw new ToolInputException("표에서는 anchor와 autofit을 쓸 수 없어요. 표의 글 모양은 단락 서식만 바꿀 수 있어요.");

            var count = target.ParagraphCount();
            foreach (var number in paragraphs ?? new List<int>())
            {
                if (number > count)
                    throw new ToolInputException($"{number.ToString(CultureInfo.InvariantCulture)}번 단락은 없어요. 이 상자에는 단락이 {count.ToString(CultureInfo.InvariantCulture)}개 있어요.");
            }

            var style = target.Style();
            if (format.StartOnly)
                CheckNumbered(style, target, paragraphs);

            var before = target.Element.OuterXml;
            var all = format.TouchesParagraphs ? target.EnsureParagraphs() : new List<A.Paragraph>();
            if (format.TouchesParagraphs)
            {
                var scope = paragraphs == null ? all : paragraphs.Select(n => all[n - 1]).ToList();
                foreach (var paragraph in scope)
                    ApplyParagraph(paragraph, format, style);
            }

            if (format.Anchor != null || format.Autofit != null)
            {
                PptSlides.EnsureTextBody(target.Shape);
                var body = target.Shape.TextBody.BodyProperties ??= new A.BodyProperties();
                if (format.Anchor != null)
                    PptBodyProps.SetAnchor(body, format.Anchor.Value);

                if (format.Autofit != null)
                    PptBodyProps.SetAutofit(body, format.Autofit.Value);
            }

            if (format.TouchesParagraphs && format.Autofit == null && target.Shape != null)
                PptSlides.ResetAutofit(target.Shape);

            var label = target.Label();
            if (target.Element.OuterXml == before)
                return ToolResult.Ok($"Slide {slide.Number} {label} already had this formatting. Nothing changed.");

            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = "paragraph",
                Anchors = { PptAnchor.Shape(slide.SlideId, target.ShapeId) },
            };

            var scopeText = paragraphs == null ? string.Empty : $" (paragraph{PptOutput.Plural(paragraphs.Count)} {string.Join(", ", paragraphs)})";
            return ToolResult.Changed($"Slide {slide.Number} {label}: {Describe(format)}{scopeText}.", change, DetailOf(format));
        }

        private static Format Parse(ToolInput input)
        {
            var format = new Format
            {
                Align = PptInputs.Enum(
                    input,
                    "align",
                    ("left|l", A.TextAlignmentTypeValues.Left),
                    ("center|centre|ctr", A.TextAlignmentTypeValues.Center),
                    ("right|r", A.TextAlignmentTypeValues.Right),
                    ("justify|justified|just", A.TextAlignmentTypeValues.Justified)),
                Bullet = PptInputs.Choice(input, "bullet", "none", "bullet", "number"),
                BulletChar = input.Has("bullet_char") ? PptBullets.CheckChar(input.GetOptionalString("bullet_char")) : null,
                NumberStyle = input.Has("number_style") ? PptBullets.ParseStyle(input.GetOptionalString("number_style")) : null,
                StartAt = PptInputs.Integer(input, "start_at", 1, PptBullets.MaxStartAt),
                Level = PptInputs.Integer(input, "level", 1, 9),
                LevelChange = PptInputs.Integer(input, "level_change", -8, 8),
                LineSpacing = PptInputs.Number(input, "line_spacing", 0.5, 9.99),
                SpaceBefore = PptInputs.Number(input, "space_before", 0, 999),
                SpaceAfter = PptInputs.Number(input, "space_after", 0, 999),
                Anchor = PptInputs.Enum(
                    input,
                    "anchor",
                    ("top|t", A.TextAnchoringTypeValues.Top),
                    ("middle|center|ctr|m", A.TextAnchoringTypeValues.Center),
                    ("bottom|b", A.TextAnchoringTypeValues.Bottom)),
                Autofit = PptInputs.Enum(
                    input,
                    "autofit",
                    ("none", PptAutofit.None),
                    ("shrink|shrink_on_overflow", PptAutofit.Shrink),
                    ("resize_shape|resize|shape", PptAutofit.Resize)),
            };

            if (format.Count == 0)
                throw new ToolInputException("바꿀 서식을 하나 이상 알려 주세요. (align, bullet, level, level_change, line_spacing, space_before, space_after, anchor, autofit)");

            if (format.Level != null && format.LevelChange != null)
                throw new ToolInputException("level과 level_change는 함께 쓸 수 없어요. 하나만 알려 주세요.");

            if (format.LevelChange == 0)
                throw new ToolInputException("level_change는 0이 될 수 없어요. 1(수준 올림)이나 -1(수준 내림)처럼 알려 주세요.");

            var numberOptions = format.NumberStyle != null || format.StartAt != null;
            if (format.BulletChar != null && numberOptions)
                throw new ToolInputException("bullet_char와 number_style, start_at은 함께 쓸 수 없어요. 글머리 기호와 번호 매기기 중 하나만 알려 주세요.");

            if (format.Bullet == "none" && (format.BulletChar != null || numberOptions))
                throw new ToolInputException("bullet이 none이면 bullet_char, number_style, start_at은 쓸 수 없어요.");

            if (format.Bullet == "bullet" && numberOptions)
                throw new ToolInputException("number_style과 start_at은 bullet이 number일 때만 쓸 수 있어요.");

            if (format.Bullet == "number" && format.BulletChar != null)
                throw new ToolInputException("bullet_char는 bullet이 bullet일 때만 쓸 수 있어요.");

            if (format.Bullet == null)
                format.Bullet = format.BulletChar != null ? "bullet" : numberOptions ? "number" : null;

            format.StartOnly = format.Bullet == "number" && input.Has("start_at") && !input.Has("bullet") && !input.Has("number_style");
            return format;
        }

        private static void CheckNumbered(PptShapeStyle style, PptTextTarget target, List<int> paragraphs)
        {
            var existing = target.Paragraphs();
            var numbers = paragraphs ?? Enumerable.Range(1, existing.Count).ToList();
            foreach (var number in numbers)
            {
                if (number > existing.Count || style.Describe(existing[number - 1]).BulletKind != "number")
                    throw new ToolInputException("start_at는 번호가 매겨진 단락에만 쓸 수 있어요. 번호를 새로 매기려면 bullet을 number로 함께 알려 주세요.");

                if (existing[number - 1].ParagraphProperties?.GetFirstChild<A.AutoNumberedBullet>() == null)
                    PptBullets.ParseStyle(style.Describe(existing[number - 1]).BulletNumberType);
            }
        }

        private static void ApplyParagraph(A.Paragraph paragraph, Format format, PptShapeStyle style)
        {
            if (format.Level != null || format.LevelChange != null)
                ApplyLevel(paragraph, format);

            switch (format.Bullet)
            {
                case "none":
                    PptBullets.ApplyNone(paragraph, style);
                    break;
                case "bullet":
                    PptBullets.ApplyChar(paragraph, style, format.BulletChar ?? PptBullets.DefaultChar);
                    break;
                case "number":
                    if (format.StartOnly)
                    {
                        PptBullets.ApplyStartAt(paragraph, format.StartAt.Value);
                        if (paragraph.ParagraphProperties?.GetFirstChild<A.AutoNumberedBullet>() == null)
                            PptBullets.ApplyNumber(paragraph, style, ParseExisting(style, paragraph), format.StartAt.Value);
                    }
                    else
                        PptBullets.ApplyNumber(paragraph, style, format.NumberStyle ?? A.TextAutoNumberSchemeValues.ArabicPeriod, format.StartAt ?? 1);

                    break;
            }

            if (format.Align != null)
                PptParagraphProps.SetAlign(PptParagraphProps.Of(paragraph), format.Align.Value);

            if (format.LineSpacing != null)
                PptParagraphProps.SetLineSpacing(PptParagraphProps.Of(paragraph), format.LineSpacing.Value);

            if (format.SpaceBefore != null)
                PptParagraphProps.SetSpaceBefore(PptParagraphProps.Of(paragraph), format.SpaceBefore.Value);

            if (format.SpaceAfter != null)
                PptParagraphProps.SetSpaceAfter(PptParagraphProps.Of(paragraph), format.SpaceAfter.Value);
        }

        private static A.TextAutoNumberSchemeValues ParseExisting(PptShapeStyle style, A.Paragraph paragraph)
        {
            var info = style.Describe(paragraph);
            return PptBullets.ParseStyle(info.BulletNumberType);
        }

        private static void ApplyLevel(A.Paragraph paragraph, Format format)
        {
            var current = PptText.LevelOf(paragraph);
            var next = format.Level != null ? format.Level.Value - 1 : Math.Clamp(current + format.LevelChange.Value, 0, 8);
            if (next == current)
                return;

            var props = PptParagraphProps.Of(paragraph);
            PptParagraphProps.SetLevel(props, next);
            if (props.LeftMargin != null && props.LeftMargin.HasValue)
                props.LeftMargin = (int)Math.Max(0, props.LeftMargin.Value + (next - current) * PptBullets.LevelStepEmu);
        }

        private static string Describe(Format format)
        {
            var parts = new List<string>();
            if (format.Bullet == "none")
                parts.Add("bullets off");
            else if (format.Bullet == "bullet")
                parts.Add($"bullets on ({format.BulletChar ?? PptBullets.DefaultChar})");
            else if (format.Bullet == "number")
            {
                var style = format.NumberStyle == null ? "kept style" : PptBullets.StyleToken(format.NumberStyle.Value);
                parts.Add($"numbering on ({style}{(format.StartAt != null ? ", start at " + format.StartAt.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)})");
            }

            if (format.Level != null)
                parts.Add("level " + format.Level.Value.ToString(CultureInfo.InvariantCulture));

            if (format.LevelChange != null)
                parts.Add("level " + (format.LevelChange.Value > 0 ? "+" : string.Empty) + format.LevelChange.Value.ToString(CultureInfo.InvariantCulture));

            if (format.Align != null)
                parts.Add("aligned " + AlignName(format.Align.Value));

            if (format.LineSpacing != null)
                parts.Add("line spacing " + PptOutput.Number(format.LineSpacing.Value));

            if (format.SpaceBefore != null)
                parts.Add("space before " + PptOutput.Points(format.SpaceBefore.Value));

            if (format.SpaceAfter != null)
                parts.Add("space after " + PptOutput.Points(format.SpaceAfter.Value));

            if (format.Anchor != null)
                parts.Add("text anchored " + (format.Anchor.Value == A.TextAnchoringTypeValues.Top ? "top" : format.Anchor.Value == A.TextAnchoringTypeValues.Bottom ? "bottom" : "middle"));

            if (format.Autofit != null)
                parts.Add("autofit " + (format.Autofit.Value == PptAutofit.None ? "off" : format.Autofit.Value == PptAutofit.Shrink ? "shrink text on overflow" : "resize shape to fit text"));

            return string.Join(", ", parts);
        }

        private static string AlignName(A.TextAlignmentTypeValues align)
        {
            if (align == A.TextAlignmentTypeValues.Center)
                return "center";

            if (align == A.TextAlignmentTypeValues.Right)
                return "right";

            return align == A.TextAlignmentTypeValues.Justified ? "justified" : "left";
        }

        private static string DetailOf(Format format)
        {
            if (format.Groups != 1)
                return "단락 모양";

            if (format.Align != null)
            {
                if (format.Align.Value == A.TextAlignmentTypeValues.Center)
                    return "가운데 맞춤";

                if (format.Align.Value == A.TextAlignmentTypeValues.Right)
                    return "오른쪽 맞춤";

                return format.Align.Value == A.TextAlignmentTypeValues.Justified ? "양쪽 맞춤" : "왼쪽 맞춤";
            }

            if (format.Bullet != null)
                return format.Bullet == "number" ? "번호 매기기" : format.Bullet == "bullet" ? "글머리 기호" : "글머리 기호 끄기";

            if (format.LevelChange != null)
                return format.LevelChange.Value > 0 ? "수준 올림" : "수준 내림";

            if (format.Level != null)
                return "수준 바꾸기";

            if (format.LineSpacing != null)
                return "줄 간격 " + format.LineSpacing.Value.ToString("0.##", CultureInfo.InvariantCulture);

            if (format.SpaceBefore != null)
                return "단락 앞 간격";

            if (format.SpaceAfter != null)
                return "단락 뒤 간격";

            return format.Anchor != null ? "세로 맞춤" : "자동 맞춤";
        }

        private sealed class Format
        {
            public A.TextAlignmentTypeValues? Align { get; init; }

            public string Bullet { get; set; }

            public string BulletChar { get; init; }

            public A.TextAutoNumberSchemeValues? NumberStyle { get; init; }

            public int? StartAt { get; init; }

            public int? Level { get; init; }

            public int? LevelChange { get; init; }

            public double? LineSpacing { get; init; }

            public double? SpaceBefore { get; init; }

            public double? SpaceAfter { get; init; }

            public A.TextAnchoringTypeValues? Anchor { get; init; }

            public PptAutofit? Autofit { get; init; }

            public bool StartOnly { get; set; }

            public bool TouchesParagraphs =>
                Align != null || Bullet != null || Level != null || LevelChange != null || LineSpacing != null || SpaceBefore != null || SpaceAfter != null;

            public int Count =>
                (Align != null ? 1 : 0) + (Bullet != null ? 1 : 0) + (BulletChar != null ? 1 : 0) + (NumberStyle != null ? 1 : 0) + (StartAt != null ? 1 : 0)
                + (Level != null ? 1 : 0) + (LevelChange != null ? 1 : 0) + (LineSpacing != null ? 1 : 0) + (SpaceBefore != null ? 1 : 0)
                + (SpaceAfter != null ? 1 : 0) + (Anchor != null ? 1 : 0) + (Autofit != null ? 1 : 0);

            public int Groups =>
                (Align != null ? 1 : 0) + (Bullet != null ? 1 : 0) + (Level != null || LevelChange != null ? 1 : 0) + (LineSpacing != null ? 1 : 0)
                + (SpaceBefore != null ? 1 : 0) + (SpaceAfter != null ? 1 : 0) + (Anchor != null ? 1 : 0) + (Autofit != null ? 1 : 0);
        }
    }
}
