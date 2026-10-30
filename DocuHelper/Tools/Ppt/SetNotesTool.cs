using System.Globalization;
using System.Linq;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Ppt
{
    public class SetNotesTool : Tool
    {
        private const int MaxLength = 20000;

        public override string Name => "set_notes";

        public override string Description =>
            "Replace the speaker notes of a slide: the presenter's script, shown only to the presenter. "
            + "Separate paragraphs with new lines. An empty text clears the notes. "
            + "Good for keeping details you take off a crowded slide.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slide": {"type": "integer", "minimum": 1, "description": "Slide number (1-based)."},
                "text": {"type": "string", "description": "The full notes text. Use \\n between paragraphs."}
              },
              "required": ["slide", "text"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var slide = PptSlides.Get(document, input.GetInt("slide"));
            if (!input.Has("text"))
                throw new ToolInputException("'text' 값이 필요해요.");

            var text = input.GetOptionalString("text") ?? string.Empty;
            if (text.Length > MaxLength)
                throw new ToolInputException("발표 메모가 너무 길어요. 줄여서 넣어 주세요.");

            var lines = PptLine.FromText(text);
            if (lines.Count == 0 && PptNotes.Body(slide.Part.NotesSlidePart) == null)
                return ToolResult.Ok($"Slide {slide.Number} has no speaker notes. Nothing changed.");

            var body = PptNotes.EnsureBody(document, slide.Part);
            var changed = PptText.SetLines(body.TextBody, lines);
            if (!changed)
                return ToolResult.Ok($"The speaker notes of slide {slide.Number} already had this text. Nothing changed.");

            var count = lines.Count(l => l.Text.Length > 0);
            var content = count == 0
                ? $"Cleared the speaker notes of slide {slide.Number}."
                : $"The speaker notes of slide {slide.Number} now have {count} paragraph{PptOutput.Plural(count)}.";

            var change = new DocuChange
            {
                Location = $"{slide.Number.ToString(CultureInfo.InvariantCulture)}번 메모",
                Icon = "note",
                Anchors = { PptAnchor.Notes(slide.SlideId) },
            };

            return ToolResult.Changed(content, change, count == 0 ? "메모 지움" : $"메모 {count}줄");
        }
    }
}
