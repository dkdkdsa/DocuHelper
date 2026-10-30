using System.Globalization;
using System.Linq;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Ppt
{
    public class AddSlideTool : Tool
    {
        private const int MaxTitleLength = 500;

        public override string Name => "add_slide";

        public override string Description =>
            "Add a new slide that uses a layout from the file's own slide master, so it matches the rest of the deck. "
            + "layout \"title_content\" (default) has a title and a bulleted body; \"title\" is a cover-style slide with a title and a subtitle, and its lines go into the subtitle. "
            + "Slides after the new one move down by one number.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "after": {"type": "integer", "minimum": 0, "description": "Put the new slide after this slide number; 0 puts it first. Default: after the last slide."},
                "layout": {"type": "string", "enum": ["title_content", "title"], "description": "Slide layout. Default title_content."},
                "title": {"type": "string", "description": "Title text."},
                "lines": {
                  "type": "array",
                  "description": "Body paragraphs (subtitle lines for the title layout). A string is a top-level paragraph; use {\"text\", \"level\"} for a sub-bullet (level 2).",
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

            var kind = KindOf(input.GetOptionalString("layout"));
            var title = PptText.Clean(input.GetOptionalString("title") ?? string.Empty).Trim();
            if (title.Length > MaxTitleLength)
                throw new ToolInputException("제목이 너무 길어요. 짧게 줄여 주세요.");

            var lines = PptLine.Parse(input, "lines", false);
            var near = after > 0 ? slides[after - 1].Part : slides.FirstOrDefault()?.Part;
            var layout = PptSlideFactory.FindLayout(document, kind, near);
            if (layout == null)
            {
                var wanted = kind == PptSlideFactory.TitleKind ? "표지(제목과 부제목)" : "제목과 본문";
                throw new ToolInputException($"이 파일의 슬라이드 마스터에는 {wanted} 레이아웃이 없어요. 다른 레이아웃으로 만들어 주세요.");
            }

            var slide = PptSlideFactory.Add(document, after, layout, title, lines);
            var info = PptReader.ReadSlide(document, slide);
            var shapes = info.Shapes.Where(s => s.IsText).ToList();
            var described = shapes.Count == 0
                ? "no text shapes"
                : string.Join(", ", shapes.Select(PptOutput.ShapeLabel));

            var content = $"Added slide {slide.Number} with layout \"{PptSlideFactory.LayoutName(layout)}\" ({described}).";
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

            return ToolResult.Changed(content, change, "새 슬라이드");
        }

        private static string KindOf(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return PptSlideFactory.ContentKind;

            return value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_') switch
            {
                "title_content" or "content" or "title_and_content" or "body" => PptSlideFactory.ContentKind,
                "title" or "cover" or "title_slide" => PptSlideFactory.TitleKind,
                _ => throw new ToolInputException("'layout' 값은 title_content 또는 title이어야 해요."),
            };
        }
    }
}
