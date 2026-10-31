namespace DocuHelperEditor.State.Papers
{
    public class SheetObject
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public SheetObjectKind Kind { get; set; }

        public SheetObjectAnchor Anchor { get; set; }

        public double Left { get; set; }

        public double Top { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double Rotation { get; set; }

        public int Order { get; set; }

        public string Alt { get; set; }

        public string Text { get; set; }

        public string ImageUrl { get; set; }

        public string ShapeKind { get; set; }

        public string FillColor { get; set; }

        public string LineColor { get; set; }

        public SheetChartModel Chart { get; set; }

        public bool IsChart => Kind == SheetObjectKind.Chart && Chart != null;

        public double Right => Left + Width;

        public double Bottom => Top + Height;
    }
}
