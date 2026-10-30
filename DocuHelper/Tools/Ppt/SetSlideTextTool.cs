using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public class SetSlideTextTool : Tool
    {
        public override string Name => "set_slide_text";

        public override string Description =>
            "Replace all text in one shape of a slide. Each item of `lines` becomes one paragraph (one bullet in a body). "
            + "The shape keeps its formatting: unchanged lines stay exactly as they were and new lines copy the formatting of the paragraph they replace. "
            + "Do not type bullet characters; the body adds them. If the slide has no title, subtitle or body shape yet but its layout has one, it is created. "
            + "The result tells you whether the text still fits; if not, shorten it, move details to the speaker notes, or reduce the size with set_font.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "shape": {"type": "string", "description": "Shape id from read_slides, e.g. \"3\", or \"title\", \"subtitle\", \"body\"."},
                "lines": {
                  "type": "array",
                  "description": "Paragraphs in order. A plain string keeps the bullet level of the paragraph at the same position; use {\"text\", \"level\"} to set the level (1 = top level, 2 = sub-bullet). An empty list clears the shape.",
                  "items": {
                    "anyOf": [
                      {"type": "string"},
                      {
                        "type": "object",
                        "properties": {
                          "text": {"type": "string"},
                          "level": {"type": "integer", "minimum": 1, "maximum": 9}
                        },
                        "required": ["text"]
                      }
                    ]
                  }
                }
              },
              "required": ["slide", "shape", "lines"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            var key = input.GetString("shape");
            var lines = PptLine.Parse(input, "lines", true);

            var shape = PptSlides.Resolve(document, slide, key, true);
            var body = PptSlides.EnsureTextBody(shape);
            var changed = PptText.SetLines(body, lines);
            if (changed)
                PptSlides.ResetAutofit(shape);

            var shapeId = shape.NonVisualShapeProperties?.NonVisualDrawingProperties?.Id?.Value ?? 0;
            var deckHeight = document.PresentationPart.Presentation.SlideSize?.Cy?.Value ?? 6858000;
            var info = PptReader.ReadSlide(document, slide).Shapes.FirstOrDefault(s => s.ShapeId == shapeId);
            var label = info == null ? $"[{shapeId}]" : PptOutput.ShapeLabel(info);
            var count = body.Elements<A.Paragraph>().Count(p => PptText.TextOf(p).Length > 0);

            if (!changed)
                return ToolResult.Ok($"Slide {slide.Number} {label} already had this text. Nothing changed.");

            var fit = PptFit.Estimate(info, deckHeight);
            var content = $"Slide {slide.Number} {label} now has {count} paragraph{PptOutput.Plural(count)}. Fit: {PptOutput.Fit(fit)}.";
            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = info != null && info.Role is PptShapeRole.Title or PptShapeRole.Subtitle ? "pencil" : "lines",
                Anchors = { PptAnchor.Shape(slide.SlideId, shapeId) },
            };

            return ToolResult.Changed(content, change, $"{count}줄");
        }
    }
}
