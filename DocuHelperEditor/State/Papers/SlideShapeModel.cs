using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public class SlideShapeModel
    {
        public string Id { get; set; }

        public string ShapeKey { get; set; }

        public string Name { get; set; }

        public string AltText { get; set; }

        public SlideShapeKind Kind { get; set; }

        public bool IsInherited { get; set; }

        public bool IsDecor { get; set; }

        public bool IsGrouped { get; set; }

        public double Left { get; set; }

        public double Top { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double Rotation { get; set; }

        public bool FlipH { get; set; }

        public bool FlipV { get; set; }

        public string Preset { get; set; } = "rect";

        public string FillKind { get; set; } = "none";

        public string FillColor { get; set; }

        public string FillColor2 { get; set; }

        public double FillAngle { get; set; }

        public int FillAlpha { get; set; } = 100;

        public string LineColor { get; set; }

        public int LineAlpha { get; set; } = 100;

        public double LineWidthPt { get; set; }

        public string LineDash { get; set; }

        public string ImageSrc { get; set; }

        public double CropLeft { get; set; }

        public double CropTop { get; set; }

        public double CropRight { get; set; }

        public double CropBottom { get; set; }

        public SlideBoxModel Text { get; set; }

        public bool TextEditable { get; set; }

        public SlideTableModel Table { get; set; }

        public SlideChartModel Chart { get; set; }

        public bool Movable => !IsInherited && !IsGrouped && !string.IsNullOrEmpty(ShapeKey);

        public bool HasGeometry => Kind is SlideShapeKind.Shape or SlideShapeKind.Line or SlideShapeKind.Other
            || (FillKind != "none" && FillKind != null)
            || LineColor != null;

        public IEnumerable<string> Lines => Text?.Lines ?? new List<string>();
    }
}
