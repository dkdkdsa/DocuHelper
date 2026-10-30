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

        public override string Name => "set_font";

        public override string Description =>
            "Change the font of all text in one shape of a slide: size in points, bold, italic, underline, and paragraph alignment. "
            + "Give only what should change. Prefer shortening text over making it much smaller; keep body text at 18pt or more when you can.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "shape": {"type": "string", "description": "Shape id from read_slides, e.g. \"3\", or \"title\", \"subtitle\", \"body\"."},
                "size": {"type": "number", "minimum": 1, "maximum": 400, "description": "Font size in points."},
                "bold": {"type": "boolean"},
                "italic": {"type": "boolean"},
                "underline": {"type": "boolean"},
                "align": {"type": "string", "enum": ["left", "center", "right"], "description": "Paragraph alignment."}
              },
              "required": ["slide", "shape"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var key = input.GetString("shape");
            var size = input.GetOptionalNumber("size");
            var bold = input.GetOptionalBool("bold");
            var italic = input.GetOptionalBool("italic");
            var underline = input.GetOptionalBool("underline");
            var align = ParseAlign(input.GetOptionalString("align"));

            if (size == null && bold == null && italic == null && underline == null && align == null)
                throw new ToolInputException("바꿀 서식을 하나 이상 알려 주세요. (size, bold, italic, underline, align)");

            if (size != null && (double.IsNaN(size.Value) || size.Value < MinSize || size.Value > MaxSize))
                throw new ToolInputException("글자 크기는 1~400pt 사이로 알려 주세요.");

            var shape = PptSlides.Resolve(document, slide, key, false);
            var body = PptSlides.EnsureTextBody(shape);
            var hundredths = size == null ? (int?)null : (int)Math.Round(size.Value * 100);
            foreach (var paragraph in body.Elements<A.Paragraph>().ToList())
                Apply(paragraph, hundredths, bold, italic, underline, align);

            if (size != null)
                PptSlides.ResetAutofit(shape);

            var shapeId = shape.NonVisualShapeProperties?.NonVisualDrawingProperties?.Id?.Value ?? 0;
            var info = PptReader.ReadSlide(document, slide).Shapes.FirstOrDefault(s => s.ShapeId == shapeId);
            var label = info == null ? $"[{shapeId}]" : PptOutput.ShapeLabel(info);
            var done = Describe(size, bold, italic, underline, align);
            var content = $"Slide {slide.Number} {label}: {done}.";
            if (size != null)
                content += $" Fit: {PptOutput.Fit(PptFit.Estimate(info, document.PresentationPart.Presentation.SlideSize?.Cy?.Value ?? 6858000))}.";

            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = IconOf(size, bold, italic, underline, align),
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

        private static void Apply(A.Paragraph paragraph, int? size, bool? bold, bool? italic, bool? underline, A.TextAlignmentTypeValues? align)
        {
            if (align != null)
            {
                paragraph.ParagraphProperties ??= new A.ParagraphProperties();
                paragraph.ParagraphProperties.Alignment = align.Value;
            }

            foreach (var child in paragraph.ChildElements.ToList())
            {
                switch (child)
                {
                    case A.Run run:
                        run.RunProperties ??= new A.RunProperties();
                        ApplyRun(run.RunProperties, size, bold, italic, underline);
                        break;
                    case A.Field field:
                        field.RunProperties ??= new A.RunProperties();
                        ApplyRun(field.RunProperties, size, bold, italic, underline);
                        break;
                    case A.Break lineBreak when size != null:
                        lineBreak.RunProperties ??= new A.RunProperties();
                        ApplyRun(lineBreak.RunProperties, size, null, null, null);
                        break;
                }
            }

            var end = paragraph.GetFirstChild<A.EndParagraphRunProperties>();
            if (end == null)
            {
                end = new A.EndParagraphRunProperties();
                paragraph.AppendChild(end);
            }

            ApplyRun(end, size, bold, italic, underline);
        }

        private static void ApplyRun(A.TextCharacterPropertiesType props, int? size, bool? bold, bool? italic, bool? underline)
        {
            if (size != null)
                props.FontSize = size.Value;

            if (bold != null)
                props.Bold = bold.Value;

            if (italic != null)
                props.Italic = italic.Value;

            if (underline != null)
                props.Underline = underline.Value ? A.TextUnderlineValues.Single : A.TextUnderlineValues.None;
        }

        private static string Describe(double? size, bool? bold, bool? italic, bool? underline, A.TextAlignmentTypeValues? align)
        {
            var parts = new List<string>();
            if (size != null)
                parts.Add("size " + PptOutput.Points(size.Value));

            if (bold != null)
                parts.Add(bold.Value ? "bold on" : "bold off");

            if (italic != null)
                parts.Add(italic.Value ? "italic on" : "italic off");

            if (underline != null)
                parts.Add(underline.Value ? "underline on" : "underline off");

            if (align != null)
                parts.Add("aligned " + (align.Value == A.TextAlignmentTypeValues.Center ? "center" : align.Value == A.TextAlignmentTypeValues.Right ? "right" : "left"));

            return string.Join(", ", parts);
        }

        private static string IconOf(double? size, bool? bold, bool? italic, bool? underline, A.TextAlignmentTypeValues? align)
        {
            var count = (size != null ? 1 : 0) + (bold != null ? 1 : 0) + (italic != null ? 1 : 0) + (underline != null ? 1 : 0) + (align != null ? 1 : 0);
            if (count != 1 || size != null)
                return "text-color";

            if (bold != null)
                return "bold";

            if (italic != null)
                return "italic";

            if (underline != null)
                return "underline";

            return align.Value == A.TextAlignmentTypeValues.Center ? "align-center" : "align-left";
        }
    }
}
