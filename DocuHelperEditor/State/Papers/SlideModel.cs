using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public class SlideModel
    {
        public string Id { get; set; }

        public SlideLayout Layout { get; set; }

        public List<SlideBoxModel> Boxes { get; set; } = new();

        public SlideChartModel Chart { get; set; }

        public List<string> Notes { get; set; } = new();

        public string NotesChangeId { get; set; }

        public List<string> NotesChangeIds { get; set; } = new();

        public bool NotesMarkCleared { get; set; }

        public bool FitChecked { get; set; }

        public SlideBoxModel FindBox(SlideBoxKind kind) => Boxes?.FirstOrDefault(b => b.Kind == kind);

        public SlideBoxModel FindBox(string boxId) => string.IsNullOrEmpty(boxId) ? null : Boxes?.FirstOrDefault(b => b.Id == boxId);

        public IEnumerable<SlideBoxModel> BoxesOf(SlideBoxKind kind) => Boxes?.Where(b => b.Kind == kind) ?? Enumerable.Empty<SlideBoxModel>();

        public bool Contains(SlideBoxModel box) => box != null && Boxes != null && Boxes.Contains(box);

        public bool HasNotesChange(string changeId) =>
            !string.IsNullOrEmpty(changeId) && (changeId == NotesChangeId || (NotesChangeIds != null && NotesChangeIds.Contains(changeId)));

        public IEnumerable<string> ChangeIds()
        {
            var seen = new HashSet<string>();
            if (Boxes != null)
            {
                foreach (var box in Boxes)
                {
                    foreach (var id in box.AllChangeIds())
                    {
                        if (seen.Add(id))
                            yield return id;
                    }
                }
            }

            if (NotesChangeIds != null)
            {
                foreach (var id in NotesChangeIds)
                {
                    if (!string.IsNullOrEmpty(id) && seen.Add(id))
                        yield return id;
                }
            }

            if (!string.IsNullOrEmpty(NotesChangeId) && seen.Add(NotesChangeId))
                yield return NotesChangeId;
        }

        public bool HasChange(string changeId) => !string.IsNullOrEmpty(changeId) && ChangeIds().Contains(changeId);

        public static SlideModel CreateEmpty()
        {
            var id = "slide-" + Guid.NewGuid().ToString("N");
            var slide = new SlideModel
            {
                Id = id,
                Layout = SlideLayout.Content,
            };

            slide.Boxes.Add(new SlideBoxModel { Id = id + "-title", Kind = SlideBoxKind.Title, FontSize = 22, PointSize = 40, Bold = true });
            slide.Boxes.Add(new SlideBoxModel { Id = id + "-body", Kind = SlideBoxKind.Body, FontSize = 15, PointSize = 24 });
            return slide;
        }
    }
}
