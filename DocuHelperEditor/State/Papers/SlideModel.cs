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

        public List<SlideShapeModel> Shapes { get; set; } = new();

        public SlideBackgroundModel Background { get; set; } = new();

        public string Transition { get; set; } = "none";

        public List<SlideAnimationModel> Animations { get; set; } = new();

        public List<SlideCommentModel> Comments { get; set; } = new();

        public SlideShapeModel FindShape(string shapeId) => string.IsNullOrEmpty(shapeId) ? null : Shapes?.FirstOrDefault(s => s.Id == shapeId);

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

        public void EnsureShapes()
        {
            if (Shapes == null)
                Shapes = new List<SlideShapeModel>();

            if (Shapes.Count > 0)
                return;

            var cover = Layout == SlideLayout.Cover;
            foreach (var box in Boxes ?? new List<SlideBoxModel>())
            {
                var place = box.Kind switch
                {
                    SlideBoxKind.Title when cover => (10, 30, 80, 20),
                    SlideBoxKind.Title => (6, 7, 88, 14),
                    SlideBoxKind.Subtitle when cover => (10, 52, 80, 12),
                    SlideBoxKind.Subtitle => (6, 22, 88, 8),
                    _ => (6, 24, Chart != null && Chart.HasBars ? 54 : 88, 66),
                };

                Shapes.Add(new SlideShapeModel
                {
                    Id = box.Id,
                    ShapeKey = box.ShapeKey,
                    Kind = SlideShapeKind.Text,
                    Left = place.Item1,
                    Top = place.Item2,
                    Width = place.Item3,
                    Height = place.Item4,
                    Text = box,
                    TextEditable = true,
                    Preset = "rect",
                });
            }

            if (Chart != null && Chart.HasBars)
            {
                Shapes.Add(new SlideShapeModel
                {
                    Id = "chart-" + Id,
                    Kind = SlideShapeKind.Chart,
                    Left = 64,
                    Top = 24,
                    Width = 30,
                    Height = 66,
                    Chart = Chart,
                    Preset = "rect",
                });
            }
        }

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
