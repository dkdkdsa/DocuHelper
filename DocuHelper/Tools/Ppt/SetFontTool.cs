using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public class SetFontTool : Tool
    {
        private const double MinSize = 1;
        private const double MaxSize = 400;

        private static readonly double[] StandardSizes =
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10.5, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 40, 44, 48, 54, 60, 66, 72, 80, 88, 96, 120, 144, 180, 240, 300, 400,
        };

        public override string Name => "set_font";

        public override string Description =>
            "Change character formatting of text in one shape: font name, size in points (or one step up/down the standard sizes), bold, italic, underline, strikethrough, color, paragraph alignment. "
            + "Give only what should change. All text of the shape changes unless `paragraphs` or `range` (characters of one paragraph) limit it. A table shape changes all its cells. "
            + "Prefer shortening text over making it much smaller; keep body text at 18pt or more.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "shape": {"type": "string", "description": "Shape id from read_slides, or title, subtitle, body."},
                "size": {"type": "number", "minimum": 1, "maximum": 400, "description": "Points."},
                "size_step": {"type": "integer", "minimum": -1, "maximum": 1, "description": "1 / -1: move each run to the next / previous standard size."},
                "name": {"type": "string", "description": "Font name, e.g. 맑은 고딕; or heading / body for the theme fonts."},
                "color": {"type": "string", "description": "#RRGGBB, theme color like accent1+40, or auto."},
                "bold": {"type": "boolean", "description": "On/off."},
                "italic": {"type": "boolean", "description": "On/off."},
                "underline": {"type": "boolean", "description": "On/off."},
                "strike": {"type": "boolean", "description": "On/off."},
                "align": {"type": "string", "enum": ["left", "center", "right"]},
                "paragraphs": {"type": "array", "description": "Paragraph numbers, 1-based. Default all.", "items": {"type": "integer", "minimum": 1}},
                "range": {
                  "type": "object",
                  "properties": {
                    "paragraph": {"type": "integer", "minimum": 1, "description": "1-based."},
                    "start": {"type": "integer", "minimum": 0, "description": "First character, 0-based."},
                    "end": {"type": "integer", "minimum": 1, "description": "End, exclusive."}
                  },
                  "required": ["paragraph", "start", "end"]
                }
              },
              "required": ["slide", "shape"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var key = input.GetString("shape");
            var format = new Format
            {
                Size = input.GetOptionalNumber("size"),
                Step = PptInputs.Integer(input, "size_step", -1, 1),
                Name = input.GetOptionalString("name"),
                Color = PptInputs.Color(input, "color"),
                Bold = input.GetOptionalBool("bold"),
                Italic = input.GetOptionalBool("italic"),
                Underline = input.GetOptionalBool("underline"),
                Strike = input.GetOptionalBool("strike"),
                Align = ParseAlign(input.GetOptionalString("align")),
            };

            var paragraphs = PptInputs.IntegerList(input, "paragraphs", 1, 9999, 1000);
            var range = PptTextRange.Parse(input);

            if (format.IsEmpty)
                throw new ToolInputException("바꿀 서식을 하나 이상 알려 주세요. (name, size, size_step, color, bold, italic, underline, strike, align)");

            if (format.Size != null && format.Step != null)
                throw new ToolInputException("size와 size_step은 함께 쓸 수 없어요. 하나만 알려 주세요.");

            if (format.Step == 0)
                throw new ToolInputException("size_step은 1(한 단계 크게)이나 -1(한 단계 작게)로 알려 주세요.");

            if (format.Size != null && (double.IsNaN(format.Size.Value) || format.Size.Value < MinSize || format.Size.Value > MaxSize))
                throw new ToolInputException("글자 크기는 1~400pt 사이로 알려 주세요.");

            if (format.Name != null)
                PptRunProps.FontTokens(format.Name);

            if (paragraphs != null && range != null)
                throw new ToolInputException("paragraphs와 range는 함께 쓸 수 없어요. range에 단락 번호가 이미 들어 있어요.");

            var target = PptTextTarget.Resolve(document, slide, key);
            if (target.IsTable && (paragraphs != null || range != null))
                throw new ToolInputException("표는 칸 전체의 글이 한꺼번에 바뀌어요. paragraphs나 range 없이 알려 주세요.");

            var count = target.ParagraphCount();
            foreach (var number in paragraphs ?? new List<int>())
                CheckParagraph(number, count);

            if (range != null)
            {
                CheckParagraph(range.Paragraph, count);
                var existing = target.Paragraphs();
                if (range.Paragraph > existing.Count)
                    throw new ToolInputException($"글자 범위 {range.Start.ToString(CultureInfo.InvariantCulture)}~{range.End.ToString(CultureInfo.InvariantCulture)}가 단락 길이(0글자)를 벗어났어요.");

                PptRunSplitter.Check(existing[range.Paragraph - 1], range);
            }

            var style = format.Step != null ? target.Style() : null;
            var all = target.EnsureParagraphs();
            if (range != null)
                ApplyRange(all[range.Paragraph - 1], range, format, style);
            else
            {
                var scope = paragraphs == null ? all : paragraphs.Select(n => all[n - 1]).ToList();
                foreach (var paragraph in scope)
                    ApplyParagraph(paragraph, format, style);
            }

            if (target.Shape != null && (format.Size != null || format.Step != null))
                PptSlides.ResetAutofit(target.Shape);

            var shapeId = target.ShapeId;
            var content = $"Slide {slide.Number} {target.Label()}: {Describe(format)}{Scope(paragraphs, range)}.";
            if (target.Shape != null && (format.Size != null || format.Step != null))
            {
                var info = target.Info();
                content += $" Fit: {PptOutput.Fit(PptFit.Estimate(info, document.PresentationPart.Presentation.SlideSize?.Cy?.Value ?? 6858000))}.";
            }

            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = IconOf(format),
                Anchors = { PptAnchor.Shape(slide.SlideId, shapeId) },
            };

            return ToolResult.Changed(content, change, "글자 모양");
        }

        private static A.TextAlignmentTypeValues? ParseAlign(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim().ToLowerInvariant() switch
            {
                "left" or "l" or "왼쪽" => A.TextAlignmentTypeValues.Left,
                "center" or "centre" or "ctr" or "middle" or "가운데" => A.TextAlignmentTypeValues.Center,
                "right" or "r" or "오른쪽" => A.TextAlignmentTypeValues.Right,
                _ => throw new ToolInputException("'align' 값은 left, center, right 중 하나여야 해요."),
            };
        }

        private static void CheckParagraph(int number, int count)
        {
            if (number > count)
                throw new ToolInputException($"{number.ToString(CultureInfo.InvariantCulture)}번 단락은 없어요. 이 상자에는 단락이 {count.ToString(CultureInfo.InvariantCulture)}개 있어요.");
        }

        private static void ApplyRange(A.Paragraph paragraph, PptTextRange range, Format format, PptShapeStyle style)
        {
            var level = PptText.LevelOf(paragraph);
            foreach (var element in PptRunSplitter.Select(paragraph, range))
            {
                switch (element)
                {
                    case A.Run run:
                        run.RunProperties ??= new A.RunProperties();
                        ApplyRun(run.RunProperties, format, style, level);
                        break;
                    case A.Field field:
                        field.RunProperties ??= new A.RunProperties();
                        ApplyRun(field.RunProperties, format, style, level);
                        break;
                    case A.Break lineBreak when format.ChangesSize:
                        lineBreak.RunProperties ??= new A.RunProperties();
                        ApplySize(lineBreak.RunProperties, format, style, level);
                        break;
                }
            }

            if (format.Align != null)
                PptParagraphProps.Of(paragraph).Alignment = format.Align.Value;

            PptRunSplitter.Merge(paragraph);
        }

        private static void ApplyParagraph(A.Paragraph paragraph, Format format, PptShapeStyle style)
        {
            var level = PptText.LevelOf(paragraph);
            if (format.Align != null)
            {
                paragraph.ParagraphProperties ??= new A.ParagraphProperties();
                paragraph.ParagraphProperties.Alignment = format.Align.Value;
            }

            foreach (var child in paragraph.ChildElements.ToList())
            {
                switch (child)
                {
                    case A.Run run:
                        run.RunProperties ??= new A.RunProperties();
                        ApplyRun(run.RunProperties, format, style, level);
                        break;
                    case A.Field field:
                        field.RunProperties ??= new A.RunProperties();
                        ApplyRun(field.RunProperties, format, style, level);
                        break;
                    case A.Break lineBreak when format.ChangesSize:
                        lineBreak.RunProperties ??= new A.RunProperties();
                        ApplySize(lineBreak.RunProperties, format, style, level);
                        break;
                }
            }

            var end = paragraph.GetFirstChild<A.EndParagraphRunProperties>();
            if (end == null)
            {
                end = new A.EndParagraphRunProperties();
                paragraph.AppendChild(end);
            }

            ApplyRun(end, format, style, level);
        }

        private static void ApplyRun(A.TextCharacterPropertiesType props, Format format, PptShapeStyle style, int level)
        {
            ApplySize(props, format, style, level);

            if (format.Bold != null)
                PptRunProps.SetBold(props, format.Bold.Value);

            if (format.Italic != null)
                PptRunProps.SetItalic(props, format.Italic.Value);

            if (format.Underline != null)
                PptRunProps.SetUnderline(props, format.Underline.Value);

            if (format.Strike != null)
                PptRunProps.SetStrike(props, format.Strike.Value);

            if (format.Name != null)
                PptRunProps.SetFont(props, format.Name);

            if (format.Color != null)
                PptRunProps.SetColor(props, format.Color);
        }

        private static void ApplySize(A.TextCharacterPropertiesType props, Format format, PptShapeStyle style, int level)
        {
            if (format.Size != null)
            {
                PptRunProps.SetSize(props, format.Size.Value);
                return;
            }

            if (format.Step == null)
                return;

            var current = style.RunValue(props, level, r => r.FontSize);
            var points = current != null && current.HasValue ? current.Value / 100.0 : style.FallbackSize(level);
            PptRunProps.SetSize(props, Step(points, format.Step.Value));
        }

        private static double Step(double current, int direction)
        {
            if (direction > 0)
            {
                foreach (var size in StandardSizes)
                {
                    if (size > current + 0.001)
                        return size;
                }

                return Math.Max(current, MaxSize);
            }

            for (var i = StandardSizes.Length - 1; i >= 0; i--)
            {
                if (StandardSizes[i] < current - 0.001)
                    return StandardSizes[i];
            }

            return current;
        }

        private static string Scope(List<int> paragraphs, PptTextRange range)
        {
            if (range != null)
                return $" (characters {range.Start.ToString(CultureInfo.InvariantCulture)}-{range.End.ToString(CultureInfo.InvariantCulture)} of paragraph {range.Paragraph.ToString(CultureInfo.InvariantCulture)})";

            return paragraphs == null ? string.Empty : $" (paragraph{PptOutput.Plural(paragraphs.Count)} {string.Join(", ", paragraphs)})";
        }

        private static string Describe(Format format)
        {
            var parts = new List<string>();
            if (format.Size != null)
                parts.Add("size " + PptOutput.Points(format.Size.Value));

            if (format.Step != null)
                parts.Add(format.Step.Value > 0 ? "size one step up" : "size one step down");

            if (format.Name != null)
                parts.Add("font " + format.Name.Trim());

            if (format.Color != null)
                parts.Add("color " + format.Color);

            if (format.Bold != null)
                parts.Add(format.Bold.Value ? "bold on" : "bold off");

            if (format.Italic != null)
                parts.Add(format.Italic.Value ? "italic on" : "italic off");

            if (format.Underline != null)
                parts.Add(format.Underline.Value ? "underline on" : "underline off");

            if (format.Strike != null)
                parts.Add(format.Strike.Value ? "strikethrough on" : "strikethrough off");

            if (format.Align != null)
                parts.Add("aligned " + (format.Align.Value == A.TextAlignmentTypeValues.Center ? "center" : format.Align.Value == A.TextAlignmentTypeValues.Right ? "right" : "left"));

            return string.Join(", ", parts);
        }

        private static string IconOf(Format format)
        {
            if (format.Count != 1 || format.Size != null)
                return "text-color";

            if (format.Bold != null)
                return "bold";

            if (format.Italic != null)
                return "italic";

            if (format.Underline != null)
                return "underline";

            if (format.Align != null)
                return format.Align.Value == A.TextAlignmentTypeValues.Center ? "align-center" : "align-left";

            return "text-color";
        }

        private sealed class Format
        {
            public double? Size { get; init; }

            public int? Step { get; init; }

            public string Name { get; init; }

            public PptColorSpec Color { get; init; }

            public bool? Bold { get; init; }

            public bool? Italic { get; init; }

            public bool? Underline { get; init; }

            public bool? Strike { get; init; }

            public A.TextAlignmentTypeValues? Align { get; init; }

            public bool ChangesSize => Size != null || Step != null;

            public bool IsEmpty => Count == 0;

            public int Count =>
                (Size != null ? 1 : 0) + (Step != null ? 1 : 0) + (Name != null ? 1 : 0) + (Color != null ? 1 : 0)
                + (Bold != null ? 1 : 0) + (Italic != null ? 1 : 0) + (Underline != null ? 1 : 0) + (Strike != null ? 1 : 0)
                + (Align != null ? 1 : 0);
        }
    }
}
