using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Tools.Ppt
{
    public class AddSlideTool : Tool
    {
        private const int MaxTitleLength = 500;

        public override string Name => "add_slide";

        public override string Description =>
            "Add a new slide that uses a layout from the file's own slide master, so it matches the rest of the deck. "
            + "`layout` is a standard kind (title, title_content, section, two_content, comparison, title_only, blank, content_caption, picture_caption), "
            + "\"same_as_current\" (the layout of the slide at `after`; a title slide gets title_content), or the exact name of a layout of this deck. "
            + "A standard kind the deck does not have yet is created in the slide master. "
            + "The `lines` fill the first text placeholder after the title (the subtitle for title layouts). "
            + "Slides after the new one move down by one number.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "after": {"type": "integer", "minimum": 0, "description": "Put the new slide after this slide number; 0 puts it first. Default: after the last slide."},
                "layout": {"type": "string", "description": "Standard kind, \"same_as_current\", or an exact layout name from read_slides detail=design. Default title_content."},
                "title": {"type": "string", "description": "Title text."},
                "lines": {
                  "type": "array",
                  "description": "Body paragraphs. A string is a top-level paragraph; use {\"text\", \"level\"} for a sub-bullet (level 2).",
                  "items": {
                    "anyOf": [
                      {"type": "string"},
                      {
                        "type": "object",
                        "properties": {
                          "text": {"type": "string"},
                          "level": {"type": "integer", "description": "Bullet level", "minimum": 1, "maximum": 9}
                        },
                        "required": ["text"]
                      }
                    ]
                  }
                }
              }
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slides = PptSlides.List(document);
            var after = input.GetOptionalInt("after") ?? slides.Count;
            if (after < 0 || after > slides.Count)
                throw new ToolInputException($"'after' 값은 0부터 {slides.Count} 사이여야 해요.");

            if (slides.Count >= PptSlideOrder.MaxSlides)
                throw new ToolInputException($"슬라이드는 {PptSlideOrder.MaxSlides.ToString(CultureInfo.InvariantCulture)}장까지만 둘 수 있어요.");

            var title = PptText.Clean(input.GetOptionalString("title") ?? string.Empty).Trim();
            if (title.Length > MaxTitleLength)
                throw new ToolInputException("제목이 너무 길어요. 짧게 줄여 주세요.");

            var lines = PptLine.Parse(input, "lines", false);
            var near = after > 0 ? slides[after - 1].Part : slides.FirstOrDefault()?.Part;
            var choice = ChooseLayout(document, input.GetOptionalString("layout"), near);
            PptSlideFactory.Materialize(document, new[] { choice });

            var layout = choice.Layout;
            var slide = PptSlideFactory.Add(document, after, layout, title, lines);
            var info = PptReader.ReadSlide(document, slide);
            var shapes = info.Shapes.Where(s => s.IsText).ToList();
            var described = shapes.Count == 0
                ? "no text shapes"
                : string.Join(", ", shapes.Select(PptOutput.ShapeLabel));

            var layoutName = PptSlideFactory.LayoutName(layout);
            var content = $"Added slide {slide.Number} with layout \"{layoutName}\" ({described}).";
            if (choice.Created)
                content += $" The deck had no such layout, so \"{layoutName}\" was created in the slide master.";

            if (slide.Number <= slides.Count)
                content += $" The slides that were {slide.Number}-{slides.Count} are now {slide.Number + 1}-{slides.Count + 1}.";

            var slideHeight = document.PresentationPart.Presentation.SlideSize?.Cy?.Value ?? 6858000;
            foreach (var shape in shapes.Where(s => s.HasText))
            {
                var fit = PptFit.Estimate(shape, slideHeight);
                if (fit.IsKnown && !fit.Fits)
                    content += $" {PptOutput.ShapeLabel(shape)}: {PptOutput.Fit(fit)}.";
            }

            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번",
                Icon = "presentation",
            };
            change.Anchors.AddRange(shapes.Select(s => s.Anchor));

            return ToolResult.Changed(content, change, choice.Created ? $"새 슬라이드 · 레이아웃 {layoutName}" : "새 슬라이드");
        }

        private static PptLayoutChoice ChooseLayout(PresentationDocument document, string request, SlidePart near)
        {
            if (PptLayoutKinds.Normalize(request) != PptLayoutKinds.SameAsCurrent)
                return PptSlideFactory.Resolve(document, request, near);

            var current = near?.SlideLayoutPart;
            if (current == null || PptLayoutKinds.Classify(current) == PptLayoutKinds.Title)
                return PptSlideFactory.Resolve(document, PptLayoutKinds.Content, near);

            return PptSlideFactory.Existing(current);
        }
    }
}
