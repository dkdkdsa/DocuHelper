namespace DocuHelper.Dash
{
    public sealed class DashStyle
    {
        public double? X { get; set; }

        public double? Y { get; set; }

        public double? W { get; set; }

        public double? H { get; set; }

        public double? Rotation { get; set; }

        public string Direction { get; set; }

        public double? Gap { get; set; }

        public double? Padding { get; set; }

        public string Align { get; set; }

        public string Justify { get; set; }

        public double? Grow { get; set; }

        public string FontName { get; set; }

        public double? FontSize { get; set; }

        public int? FontWeight { get; set; }

        public bool? Italic { get; set; }

        public bool? Underline { get; set; }

        public string Color { get; set; }

        public string TextAlign { get; set; }

        public string Mark { get; set; }

        public string Fill { get; set; }

        public string Line { get; set; }

        public double? LineWidth { get; set; }

        public string LineDash { get; set; }

        public double? Radius { get; set; }

        public double? Opacity { get; set; }

        public DashStyle Clone() => (DashStyle)MemberwiseClone();

        public DashStyle Merge(DashStyle over)
        {
            if (over == null)
                return Clone();

            return new DashStyle
            {
                X = over.X ?? X,
                Y = over.Y ?? Y,
                W = over.W ?? W,
                H = over.H ?? H,
                Rotation = over.Rotation ?? Rotation,
                Direction = over.Direction ?? Direction,
                Gap = over.Gap ?? Gap,
                Padding = over.Padding ?? Padding,
                Align = over.Align ?? Align,
                Justify = over.Justify ?? Justify,
                Grow = over.Grow ?? Grow,
                FontName = over.FontName ?? FontName,
                FontSize = over.FontSize ?? FontSize,
                FontWeight = over.FontWeight ?? FontWeight,
                Italic = over.Italic ?? Italic,
                Underline = over.Underline ?? Underline,
                Color = over.Color ?? Color,
                TextAlign = over.TextAlign ?? TextAlign,
                Mark = over.Mark ?? Mark,
                Fill = over.Fill ?? Fill,
                Line = over.Line ?? Line,
                LineWidth = over.LineWidth ?? LineWidth,
                LineDash = over.LineDash ?? LineDash,
                Radius = over.Radius ?? Radius,
                Opacity = over.Opacity ?? Opacity,
            };
        }
    }
}
