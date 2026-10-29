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

        public string Footer { get; set; }

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

            ActiveIndex = Math.Clamp(ActiveIndex, 0, Slides.Count - 1);
            if (SelectedBox == null)
                SelectedBoxId = null;
        }

        public static PptPaperModel CreateEmpty()
        {
            var model = new PptPaperModel();
            model.Slides.Add(SlideModel.CreateEmpty());
            return model;
        }
    }
}
