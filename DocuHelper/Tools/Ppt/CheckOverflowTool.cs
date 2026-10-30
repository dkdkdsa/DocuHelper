using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Ppt
{
    public class CheckOverflowTool : Tool
    {
        private const int MaxReported = 40;

        public override string Name => "check_overflow";

        public override string Description =>
            "Estimate whether the text fits inside each text shape, from the shape size, the font size and the text length "
            + "(Korean characters count as full width, Latin as about half). Reports shapes with too much text, with estimated lines vs room. "
            + "Use it after adding or lengthening text. It is an estimate, so leave a little margin.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number to check. Default: all slides."}
              }
            }
            """;

        public override bool IsReadOnly => true;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var deck = PptReader.Read(document);
            if (deck.Slides.Count == 0)
                return ToolResult.Ok("There are no slides to check.", "슬라이드 0장");

            var number = input.GetOptionalInt("slide");
            if (number != null && (number < 1 || number > deck.Slides.Count))
                throw new ToolInputException($"{number}번 슬라이드는 없어요. 슬라이드는 1번부터 {deck.Slides.Count}번까지 있어요.");

            var slides = number == null ? deck.Slides : new List<PptSlideInfo> { deck.Slides[number.Value - 1] };
            var checkedCount = 0;
            var overflowing = new List<string>();
            var fitting = new List<string>();
            var unknown = 0;

            foreach (var slide in slides)
            {
                foreach (var shape in slide.Shapes.Where(s => s.IsText && s.HasText && s.Role != PptShapeRole.Footer))
                {
                    var fit = PptFit.Estimate(shape, deck.SlideHeight);
                    if (!fit.IsKnown)
                    {
                        unknown++;
                        continue;
                    }

                    checkedCount++;
                    var line = $"- Slide {slide.Number} {PptOutput.ShapeLabel(shape)}: {PptOutput.Fit(fit)}";
                    if (fit.Fits)
                        fitting.Add(line);
                    else
                        overflowing.Add(line);
                }
            }

            var scope = number == null ? $"all {deck.Slides.Count} slides" : $"slide {number}";
            var text = new StringBuilder();
            text.AppendLine($"Checked {checkedCount} text shape{PptOutput.Plural(checkedCount)} on {scope}.");
            if (overflowing.Count == 0)
            {
                text.AppendLine("All text fits.");
            }
            else
            {
                text.AppendLine("Too much text:");
                foreach (var line in overflowing.Take(MaxReported))
                    text.AppendLine(line);

                if (overflowing.Count > MaxReported)
                    text.AppendLine($"… and {overflowing.Count - MaxReported} more.");
            }

            if (number != null && fitting.Count > 0)
            {
                text.AppendLine("Fits:");
                foreach (var line in fitting)
                    text.AppendLine(line);
            }

            if (unknown > 0)
                text.AppendLine($"{unknown} shape{PptOutput.Plural(unknown)} without a known size were skipped.");

            var detail = overflowing.Count == 0 ? "넘침 없음" : $"넘침 {overflowing.Count}곳";
            return ToolResult.Ok(text.ToString().TrimEnd(), detail);
        }
    }
}
