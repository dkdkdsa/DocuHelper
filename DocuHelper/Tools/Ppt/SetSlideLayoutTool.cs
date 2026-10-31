using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Tools.Ppt
{
    public class SetSlideLayoutTool : Tool
    {
        public override string Name => "set_slide_layout";

        public override string Description =>
            "Change the layout of slides, or reset their placeholders. "
            + "mode \"apply\" (default) switches the slides to `layout` (a standard kind or the exact name of a layout of this deck): "
            + "text in title and body placeholders moves into the matching placeholders of the new layout, extra text stays as it is. "
            + "mode \"reset\" puts the placeholders of the slides back to the position, size and formatting of their layout (text is kept).";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "slides": {
                  "description": "Slide numbers (1-based), or \"all\".",
                  "anyOf": [
                    {"type": "string", "enum": ["all"]},
                    {"type": "array", "items": {"type": "integer", "minimum": 1}}
                  ]
                },
                "mode": {"type": "string", "enum": ["apply", "reset"], "description": "Default apply."},
                "layout": {"type": "string", "description": "Standard kind (title, title_content, section, two_content, comparison, title_only, blank, content_caption, picture_caption) or an exact layout name. Needed for apply."}
              },
              "required": ["slides"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var mode = PptInputs.Choice(input, "mode", "apply", "reset") ?? "apply";
            var selected = PptSlideSet.Parse(document, input, "slides", true);
            var total = PptSlides.List(document).Count;

            return mode == "reset" ? Reset(document, selected, total) : Apply(document, input, selected, total);
        }

        private static ToolResult Apply(PresentationDocument document, ToolInput input, List<PptSlideRef> selected, int total)
        {
            var request = input.GetOptionalString("layout");
            if (string.IsNullOrWhiteSpace(request))
                throw new ToolInputException("바꿀 레이아웃 'layout' 값이 필요해요. title_content, two_content처럼 표준 이름이나 이 파일의 레이아웃 이름을 알려 주세요.");

            if (PptLayoutKinds.Normalize(request) == PptLayoutKinds.SameAsCurrent)
                throw new ToolInputException("set_slide_layout에서는 'same_as_current'를 쓸 수 없어요. 바꿀 레이아웃을 알려 주세요.");

            var choices = selected.Select(s => PptSlideFactory.Resolve(document, request, s.Part)).ToList();
            PptSlideFactory.Materialize(document, choices);

            var results = new List<PptRemapResult>();
            for (var i = 0; i < selected.Count; i++)
                results.Add(PptPlaceholderMapper.Apply(document, selected[i], choices[i].Layout));

            var name = PptSlideFactory.LayoutName(choices[0].Layout);
            var created = choices.Where(c => c.Created).Select(c => PptSlideFactory.LayoutName(c.Layout)).Distinct().ToList();
            var numbers = PptSlideOrder.Ranges(selected.Select(s => s.Number));

            if (results.All(r => !r.IsChanged && r.Created == 0) && created.Count == 0)
                return ToolResult.Ok($"{Slides(selected.Count)} {numbers} already use{(selected.Count == 1 ? "s" : string.Empty)} layout \"{name}\". Nothing changed.");

            var content = $"{Slides(selected.Count)} {numbers} now use{(selected.Count == 1 ? "s" : string.Empty)} layout \"{name}\".";
            var converted = results.Sum(r => r.Converted);
            var dropped = results.Sum(r => r.Dropped);
            var added = results.Sum(r => r.Created);
            if (converted > 0)
                content += $" {converted} text shape{PptOutput.Plural(converted)} without a matching placeholder {(converted == 1 ? "was" : "were")} kept as ordinary text box{(converted == 1 ? string.Empty : "es")}.";

            if (dropped > 0)
                content += $" {dropped} empty placeholder{PptOutput.Plural(dropped)} the new layout has no place for {(dropped == 1 ? "was" : "were")} removed.";

            if (added > 0)
                content += $" {added} empty placeholder{PptOutput.Plural(added)} from the layout {(added == 1 ? "was" : "were")} added.";

            if (created.Count > 0)
                content += $" Created layout{(created.Count == 1 ? string.Empty : "s")} {string.Join(", ", created.Select(n => "\"" + n + "\""))} in the slide master.";

            var change = new DocuChange
            {
                Location = PptSlideSet.Location(selected, total),
                Icon = "layout",
            };

            change.Anchors.AddRange(PptSlideSet.Anchors(selected));
            for (var i = 0; i < selected.Count; i++)
                change.Anchors.AddRange(results[i].Changed.Select(id => PptAnchor.Shape(selected[i].SlideId, id)));

            return ToolResult.Changed(content, change, $"레이아웃 {name}");
        }

        private static ToolResult Reset(PresentationDocument document, List<PptSlideRef> selected, int total)
        {
            var changedShapes = new List<(PptSlideRef Slide, List<uint> Ids)>();
            foreach (var slide in selected)
                changedShapes.Add((slide, PptPlaceholderMapper.Reset(document, slide)));

            var touched = changedShapes.Where(c => c.Ids.Count > 0).ToList();
            var numbers = PptSlideOrder.Ranges(selected.Select(s => s.Number));
            if (touched.Count == 0)
                return ToolResult.Ok($"The placeholders of {Slides(selected.Count).ToLowerInvariant()} {numbers} already match their layout. Nothing changed.");

            var count = touched.Sum(c => c.Ids.Count);
            var content = $"Reset {count} placeholder{PptOutput.Plural(count)} on {Slides(touched.Count).ToLowerInvariant()} {PptSlideOrder.Ranges(touched.Select(t => t.Slide.Number))} to the position, size and formatting of their layout. Text is unchanged.";

            var change = new DocuChange
            {
                Location = PptSlideSet.Location(touched.Select(t => t.Slide).ToList(), total),
                Icon = "layout",
            };

            foreach (var item in touched)
            {
                change.Anchors.Add(PptAnchor.Slide(item.Slide.SlideId));
                change.Anchors.AddRange(item.Ids.Select(id => PptAnchor.Shape(item.Slide.SlideId, id)));
            }

            return ToolResult.Changed(content, change, "다시 설정");
        }

        private static string Slides(int count) => count == 1 ? "Slide" : "Slides";
    }
}
