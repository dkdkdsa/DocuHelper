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

        public bool NotesMarkCleared { get; set; }

        public bool FitChecked { get; set; }

        public SlideBoxModel FindBox(SlideBoxKind kind) => Boxes?.FirstOrDefault(b => b.Kind == kind);

        public bool Contains(SlideBoxModel box) => box != null && Boxes != null && Boxes.Contains(box);

        public IEnumerable<string> ChangeIds()
        {
            if (Boxes != null)
            {
                foreach (var box in Boxes)
                {
                    if (!string.IsNullOrEmpty(box.ChangeId))
                        yield return box.ChangeId;
                }
            }

            if (!string.IsNullOrEmpty(NotesChangeId))
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

            slide.Boxes.Add(new SlideBoxModel { Id = id + "-title", Kind = SlideBoxKind.Title, FontSize = 22, Bold = true });
            slide.Boxes.Add(new SlideBoxModel { Id = id + "-body", Kind = SlideBoxKind.Body, FontSize = 15 });
            return slide;
        }
    }
}
