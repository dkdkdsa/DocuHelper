using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelper.Tools.Ppt
{
    public class ArrangeSlidesTool : Tool
    {
        public override string Name => "arrange_slides";

        public override string Description =>
            "Duplicate, delete, move, hide or show slides. `slides` are slide numbers. "
            + "duplicate puts the copies right after the last selected slide (or after slide `to`); "
            + "move puts the selected slides, in their current order, after slide `to` (0 = first); "
            + "delete removes them (notes and comments go with them); "
            + "hide/show toggles whether the slide is skipped in the slide show. "
            + "Slide numbers shift afterwards; the result tells the new numbers.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "action": {"type": "string", "enum": ["duplicate", "delete", "move", "hide", "show"]},
                "slides": {
                  "description": "Slide numbers (1-based). \"all\" only for hide and show.",
                  "anyOf": [
                    {"type": "string", "enum": ["all"]},
                    {"type": "array", "items": {"type": "integer", "minimum": 1}}
                  ]
                },
                "to": {"type": "integer", "minimum": 0, "description": "duplicate and move: put the result after this slide number; 0 = first. Default for duplicate: right after the last selected slide."}
              },
              "required": ["action", "slides"]
            }
            """;

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            var document = PptSlides.DocumentOf(docu);
            var action = PptInputs.Choice(input, "action", "duplicate", "delete", "move", "hide", "show")
                ?? throw new ToolInputException("'action' 값이 필요해요. duplicate, delete, move, hide, show 중 하나로 알려 주세요.");

            var all = PptSlides.List(document);
            var selected = PptSlideSet.Parse(document, input, "slides", action is "hide" or "show");
            var to = input.GetOptionalInt("to");
            if (to != null && (to < 0 || to > all.Count))
                throw new ToolInputException($"'to' 값은 0부터 {all.Count} 사이여야 해요.");

            return action switch
            {
                "duplicate" => Duplicate(document, all, selected, to),
                "delete" => Delete(document, all, selected),
                "move" => Move(document, all, selected, to ?? throw new ToolInputException("옮길 위치 'to' 값이 필요해요. 몇 번 슬라이드 뒤로 옮길지 알려 주세요. 맨 앞은 0이에요.")),
                _ => Visibility(all, selected, action == "hide"),
            };
        }

        private static ToolResult Duplicate(PresentationDocument document, List<PptSlideRef> all, List<PptSlideRef> selected, int? to)
        {
            if (all.Count + selected.Count > PptSlideOrder.MaxSlides)
                throw new ToolInputException($"슬라이드는 {PptSlideOrder.MaxSlides.ToString(CultureInfo.InvariantCulture)}장까지만 둘 수 있어요. 복제할 장수를 줄여 주세요.");

            var anchor = to ?? selected[^1].Number;
            var parts = new List<SlidePart>();
            try
            {
                foreach (var slide in selected)
                    parts.Add(PptSlideCloner.Clone(document, slide.Part));
            }
            catch
            {
                foreach (var part in parts)
                    document.PresentationPart.DeletePart(part);

                throw;
            }

            var created = new List<PptSlideRef>();
            for (var i = 0; i < parts.Count; i++)
                created.Add(PptSlideOrder.Insert(document, parts[i], anchor + i));

            var sources = selected.Select(s => s.Number).ToList();
            var numbers = created.Select(c => c.Number).ToList();
            var shifted = all.Where(s => s.Number > anchor).Select(s => (s.Number, s.Number + parts.Count));
            var skipped = selected.Sum(s => PptSlideCloner.SkippedModernComments(s.Part));

            var content = numbers.Count == 1
                ? $"Slide {numbers[0]} is a copy of {sources[0]}."
                : $"Slides {PptSlideOrder.Ranges(numbers)} are copies of {PptSlideOrder.Ranges(sources)}.";

            content += PptSlideOrder.Renumbering(shifted);
            if (skipped > 0)
                content += " Modern comments are not copied to the new slides.";

            var change = new DocuChange
            {
                Location = PptSlideSet.Location(created, all.Count + parts.Count),
                Icon = "copy",
            };
            change.Anchors.AddRange(PptSlideSet.Anchors(created));
            return ToolResult.Changed(content, change, $"슬라이드 {parts.Count.ToString(CultureInfo.InvariantCulture)}장 복제");
        }

        private static ToolResult Delete(PresentationDocument document, List<PptSlideRef> all, List<PptSlideRef> selected)
        {
            var deleted = selected.Select(s => s.Number).ToList();
            var links = 0;
            foreach (var slide in selected.OrderByDescending(s => s.Number))
                links += PptSlideOrder.Remove(document, slide);

            PptSlideOrder.FitShowRange(document.PresentationPart, deleted, all.Count);
            var kept = all.Where(s => !deleted.Contains(s.Number)).Select(s => (s.Number, s.Number - deleted.Count(d => d < s.Number)));

            var content = deleted.Count == 1
                ? $"Deleted slide {deleted[0]}."
                : $"Deleted slides {PptSlideOrder.Ranges(deleted)}.";

            content += PptSlideOrder.Renumbering(kept);
            if (links > 0)
                content += $" Removed {links} link{PptOutput.Plural(links)} on other slides that jumped to the deleted slides.";

            if (all.Count == deleted.Count)
                content += " The deck has no slides now; use add_slide to create one.";

            var change = new DocuChange
            {
                Location = PptSlideSet.Location(selected, all.Count),
                Icon = "trash",
            };

            return ToolResult.Changed(content, change, $"슬라이드 {deleted.Count.ToString(CultureInfo.InvariantCulture)}장 지움");
        }

        private static ToolResult Move(PresentationDocument document, List<PptSlideRef> all, List<PptSlideRef> selected, int to)
        {
            var chosen = new HashSet<uint>(selected.Select(s => s.SlideId));
            var rest = all.Where(s => !chosen.Contains(s.SlideId)).ToList();
            var at = rest.Count(s => s.Number <= to);
            var order = rest.Take(at).Concat(selected).Concat(rest.Skip(at)).ToList();
            var label = PptSlideOrder.Ranges(selected.Select(s => s.Number));

            if (order.Select(s => s.SlideId).SequenceEqual(all.Select(s => s.SlideId)))
                return ToolResult.Ok($"Slide{(selected.Count == 1 ? string.Empty : "s")} {label} already {(selected.Count == 1 ? "is" : "are")} in that place. Nothing changed.");

            PptSlideOrder.Reorder(document, order, chosen);
            var newNumber = order.Select((s, i) => (s.SlideId, Number: i + 1)).ToDictionary(p => p.SlideId, p => p.Number);
            var moved = order.Where(s => chosen.Contains(s.SlideId)).Select(s => new PptSlideRef(newNumber[s.SlideId], s.Element, s.Part)).ToList();
            var others = rest.Select(s => (s.Number, newNumber[s.SlideId]));

            var where = to == 0 ? "to the front" : $"after slide {to}";
            var content = $"Moved slide{(selected.Count == 1 ? string.Empty : "s")} {label} {where}. {(selected.Count == 1 ? "It is" : "They are")} now {(moved.Count == 1 ? "slide " : "slides ")}{PptSlideOrder.Ranges(moved.Select(m => m.Number))}.";
            content += PptSlideOrder.Renumbering(others);

            var change = new DocuChange
            {
                Location = PptSlideSet.Location(moved, all.Count),
                Icon = "move",
            };
            change.Anchors.AddRange(PptSlideSet.Anchors(moved));
            return ToolResult.Changed(content, change, $"슬라이드 {selected.Count.ToString(CultureInfo.InvariantCulture)}장 옮김");
        }

        private static ToolResult Visibility(List<PptSlideRef> all, List<PptSlideRef> selected, bool hide)
        {
            var changed = selected.Where(s => IsHidden(s) != hide).ToList();
            var verb = hide ? "hidden" : "shown";
            if (changed.Count == 0)
                return ToolResult.Ok($"Slide{(selected.Count == 1 ? " " : "s ")}{PptSlideOrder.Ranges(selected.Select(s => s.Number))} {(selected.Count == 1 ? "is" : "are")} already {verb}. Nothing changed.");

            foreach (var slide in changed)
                slide.Part.Slide.Show = hide ? false : null;

            var numbers = PptSlideOrder.Ranges(changed.Select(s => s.Number));
            var content = hide
                ? $"Hid slide{(changed.Count == 1 ? string.Empty : "s")} {numbers}; {(changed.Count == 1 ? "it is" : "they are")} skipped in the slide show."
                : $"Unhid slide{(changed.Count == 1 ? string.Empty : "s")} {numbers}.";

            var change = new DocuChange
            {
                Location = PptSlideSet.Location(changed, all.Count),
                Icon = hide ? "eye-off" : "eye",
            };
            change.Anchors.AddRange(PptSlideSet.Anchors(changed));
            return ToolResult.Changed(content, change, hide ? "슬라이드 숨김" : "숨김 풀기");
        }

        private static bool IsHidden(PptSlideRef slide)
        {
            var show = slide.Part.Slide?.Show;
            return show != null && show.HasValue && !show.Value;
        }
    }
}
