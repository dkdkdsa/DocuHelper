using System;
using System.Linq;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public sealed class PptRibbonSelection
    {
        public int SlideCount { get; set; }

        public int SlideNumber { get; set; }

        public string ShapeKey { get; set; }

        public int ShapeCount { get; set; }

        public double PointSize { get; set; }

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public bool Strike { get; set; }

        public string Align { get; set; }

        public string Bullet { get; set; } = "none";

        public string TransitionEffect { get; set; } = "none";

        public string AnimationEffect { get; set; } = "none";

        public bool Hidden { get; set; }

        public bool UseTimings { get; set; } = true;

        public bool MediaControls { get; set; } = true;

        public bool AdvanceOnClick { get; set; } = true;

        public bool HasSlide => SlideNumber > 0;

        public bool HasShape => !string.IsNullOrEmpty(ShapeKey);

        public bool HasText => HasShape;

        public static PptRibbonSelection From(PptPaperModel model)
        {
            var selection = new PptRibbonSelection();
            if (model?.Slides == null)
                return selection;

            selection.SlideCount = model.Slides.Count(IsRealSlide);
            var slide = model.ActiveSlide;
            if (!IsRealSlide(slide))
                return selection;

            selection.SlideNumber = model.Slides.IndexOf(slide) + 1;
            var box = model.SelectedBox;
            if (box == null || string.IsNullOrEmpty(box.ShapeKey))
                return selection;

            selection.ShapeKey = box.ShapeKey;
            selection.ShapeCount = 1;
            selection.PointSize = box.PointSize;
            selection.Bold = box.Bold;
            selection.Italic = box.Italic;
            selection.Underline = box.Underline;
            selection.Align = box.Align == SlideTextAlign.Center ? "center" : "left";
            return selection;
        }

        public static bool IsRealSlide(SlideModel slide) =>
            slide?.Id != null && slide.Id.StartsWith("s:", StringComparison.Ordinal);
    }
}
