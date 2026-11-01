using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public class PptPaperModel : PaperModel
    {
        public List<SlideModel> Slides { get; set; } = new();

        public int ActiveIndex { get; set; }

        public string SelectedBoxId { get; set; }

        public List<string> SelectedShapeIds { get; set; } = new();

        public List<SlideSectionModel> Sections { get; set; } = new();

        public double SlideWidthCm { get; set; } = 33.867;

        public double SlideHeightCm { get; set; } = 19.05;

        public bool UseTimings { get; set; } = true;

        public bool MediaControls { get; set; } = true;

        public double AspectRatio => SlideHeightCm > 0 ? SlideWidthCm / SlideHeightCm : 16.0 / 9.0;

        public string Footer { get; set; }

        public override bool IsBlank => Slides == null || Slides.All(s =>
            s.Chart == null
            && (s.Boxes == null || s.Boxes.All(b => b.IsEmpty))
            && (s.Notes == null || s.Notes.All(string.IsNullOrWhiteSpace)));

        public SlideModel ActiveSlide => Slides == null || Slides.Count == 0
            ? null
            : Slides[Math.Clamp(ActiveIndex, 0, Slides.Count - 1)];

        public SlideBoxModel SelectedBox => string.IsNullOrEmpty(SelectedBoxId)
            ? null
            : ActiveSlide?.Boxes?.FirstOrDefault(b => b.Id == SelectedBoxId);

        public int IndexOfChange(string changeId) => Slides == null ? -1 : Slides.FindIndex(s => s.HasChange(changeId));

        public SlideModel SlideOf(SlideBoxModel box) => box == null ? null : Slides?.FirstOrDefault(s => s.Contains(box));

        public void EnsureSlide()
        {
            Slides ??= new List<SlideModel>();
            if (Slides.Count == 0)
                Slides.Add(SlideModel.CreateEmpty());

            foreach (var slide in Slides)
                slide.EnsureShapes();

            ActiveIndex = Math.Clamp(ActiveIndex, 0, Slides.Count - 1);
            SyncSelection();
        }

        public void SelectShape(string shapeId, bool additive)
        {
            SelectedShapeIds ??= new List<string>();
            SelectedBoxId = null;
            var exists =!string.IsNullOrEmpty(shapeId) && ActiveSlide?.FindShape(shapeId) != null;
            if (!exists)
            {
                if (!additive)
                    SelectedShapeIds.Clear();
            }
            else if (additive)
            {
                if (!SelectedShapeIds.Remove(shapeId))
                    SelectedShapeIds.Add(shapeId);
            }
            else
            {
                SelectedShapeIds.Clear();
                SelectedShapeIds.Add(shapeId);
            }

            SyncSelection();
        }

        public void SyncSelection()
        {
            SelectedShapeIds ??= new List<string>();
            SelectedShapeIds.RemoveAll(id => ActiveSlide?.FindShape(id) == null);
            if (SelectedShapeIds.Count == 0 && ActiveSlide?.FindShape(SelectedBoxId) != null)
                SelectedShapeIds.Add(SelectedBoxId);

            SelectedBoxId = SelectedShapeIds.Count == 1 ? ActiveSlide?.FindShape(SelectedShapeIds[0])?.Text?.Id : null;
        }

        public static PptPaperModel CreateEmpty()
        {
            var model = new PptPaperModel();
            model.Slides.Add(SlideModel.CreateEmpty());
            return model;
        }
    }
}
