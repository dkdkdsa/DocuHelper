using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocuHelper.Docus;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public class SetSlideLinkTool : Tool
    {
        public override string Name => "set_slide_link";

        public override string Description =>
            "Add, change or remove a hyperlink on text or on a whole shape. "
            + "target.type: url, email, slide (jump to a slide number), previous_slide, next_slide, first_slide, last_slide, none (remove). "
            + "scope text links `range` (or all text of the shape); scope shape makes the whole shape clickable in the slide show.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "shape": {"type": "string", "description": "Shape id from read_slides, or title, subtitle, body."},
                "scope": {"type": "string", "enum": ["text", "shape"], "description": "Default text when the shape has text."},
                "range": {
                  "type": "object",
                  "properties": {
                    "paragraph": {"type": "integer", "minimum": 1, "description": "1-based."},
                    "start": {"type": "integer", "minimum": 0, "description": "First character, 0-based."},
                    "end": {"type": "integer", "minimum": 1, "description": "End, exclusive."}
                  },
                  "required": ["paragraph", "start", "end"]
                },
                "target": {
                  "type": "object",
                  "properties": {
                    "type": {"type": "string", "enum": ["url", "email", "slide", "previous_slide", "next_slide", "first_slide", "last_slide", "none"]},
                    "url": {"type": "string", "description": "https:// is added when missing."},
                    "address": {"type": "string", "description": "E-mail address."},
                    "subject": {"type": "string", "description": "Mail subject."},
                    "slide": {"type": "integer", "minimum": 1, "description": "Slide number."},
                    "tooltip": {"type": "string", "description": "Text shown on hover."}
                  },
                  "required": ["type"]
                }
              },
              "required": ["slide", "shape", "target"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var key = input.GetString("shape");
            var spec = PptLinkSpec.Parse(input);
            var scope = PptInputs.Choice(input, "scope", "text", "shape");
            var range = PptTextRange.Parse(input);

            PptLinkWriter.Check(document, spec);

            var element = PptTargets.Find(slide, key);
            var target = PptTextTarget.Of(document, slide, element);
            var paragraphs = target.Paragraphs();
            var hasText = paragraphs.Any(p => PptTextRange.LengthOf(p) > 0);

            if (range != null)
            {
                if (scope == "shape")
                    throw new ToolInputException("range는 글에 링크를 걸 때만 쓸 수 있어요. scope를 text로 하거나 range를 빼 주세요.");

                if (target.IsTable)
                    throw new ToolInputException("표는 칸 전체의 글에 한꺼번에 링크가 걸려요. range 없이 알려 주세요.");

                if (range.Paragraph > paragraphs.Count)
                    throw new ToolInputException($"{range.Paragraph.ToString(CultureInfo.InvariantCulture)}번 단락은 없어요. 이 상자에는 단락이 {paragraphs.Count.ToString(CultureInfo.InvariantCulture)}개 있어요.");

                PptRunSplitter.Check(paragraphs[range.Paragraph - 1], range);
            }

            var text = scope == "text" || (scope == null && (range != null || hasText));
            var shape = scope == "shape" || (scope == null && range == null && !hasText);
            if (spec.IsNone && scope == null && range == null)
            {
                text = hasText;
                shape = true;
            }

            if (!spec.IsNone && text && !hasText)
                throw new ToolInputException("이 상자에는 링크를 걸 글이 없어요. scope를 shape로 하면 상자 전체에 링크를 걸 수 있어요.");

            var changed = spec.IsNone
                ? Remove(slide, element, paragraphs, range, text, shape)
                : Add(document, slide, element, paragraphs, range, spec, text, shape);

            if (changed == 0)
                return ToolResult.Ok($"Slide {slide.Number} {target.Label()} has no link there. Nothing changed.");

            var shapeId = target.ShapeId;
            var content = spec.IsNone
                ? $"Slide {slide.Number} {target.Label()}: removed {Places(changed)}."
                : $"Slide {slide.Number} {target.Label()}: {Where(text, shape, range)} now links to {Destination(spec)}.";

            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = "link",
                Anchors = { PptAnchor.Shape(slide.SlideId, shapeId) },
            };

            return ToolResult.Changed(content, change, spec.IsNone ? "링크 지움" : "링크");
        }

        private static int Add(
            PresentationDocument document,
            PptSlideRef slide,
            OpenXmlCompositeElement element,
            List<A.Paragraph> paragraphs,
            PptTextRange range,
            PptLinkSpec spec,
            bool text,
            bool shape)
        {
            var link = PptLinkWriter.Create(document, slide, spec);
            var count = 0;
            if (text)
            {
                if (range != null)
                {
                    var paragraph = paragraphs[range.Paragraph - 1];
                    count += PptLinkWriter.ApplyToText(PptRunSplitter.Select(paragraph, range), link);
                    PptRunSplitter.Merge(paragraph);
                }
                else
                {
                    foreach (var paragraph in paragraphs)
                        count += PptLinkWriter.ApplyToText(PptRunSplitter.SelectAll(paragraph), link);
                }
            }

            if (shape)
            {
                PptLinkWriter.ApplyToShape(element, (A.HyperlinkOnClick)link.CloneNode(true));
                count++;
            }

            PptLinkWriter.Prune(slide);
            return count;
        }

        private static int Remove(PptSlideRef slide, OpenXmlCompositeElement element, List<A.Paragraph> paragraphs, PptTextRange range, bool text, bool shape)
        {
            var count = 0;
            if (text)
            {
                if (range != null)
                {
                    var paragraph = paragraphs[range.Paragraph - 1];
                    count += PptLinkWriter.ClearText(PptRunSplitter.Select(paragraph, range));
                    PptRunSplitter.Merge(paragraph);
                }
                else
                {
                    foreach (var paragraph in paragraphs)
                        count += PptLinkWriter.ClearText(PptRunSplitter.SelectAll(paragraph));
                }
            }

            if (shape && PptLinkWriter.ClearShape(element))
                count++;

            if (count > 0)
                PptLinkWriter.Prune(slide);

            return count;
        }

        private static string Where(bool text, bool shape, PptTextRange range)
        {
            if (text && shape)
                return "the text and the whole shape";

            if (shape)
                return "the whole shape";

            return range == null ? "all text" : $"characters {range.Start.ToString(CultureInfo.InvariantCulture)}-{range.End.ToString(CultureInfo.InvariantCulture)} of paragraph {range.Paragraph.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string Places(int count) => $"{count.ToString(CultureInfo.InvariantCulture)} link{PptOutput.Plural(count)}";

        private static string Destination(PptLinkSpec spec) => spec.Type switch
        {
            "url" or "email" => spec.Url,
            "slide" => "slide " + spec.Slide.ToString(CultureInfo.InvariantCulture),
            "previous_slide" => "the previous slide",
            "next_slide" => "the next slide",
            "first_slide" => "the first slide",
            _ => "the last slide",
        };
    }
}
