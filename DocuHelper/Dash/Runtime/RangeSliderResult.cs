namespace DocuHelper.Dash.Runtime
{
    public sealed class RangeSliderResult : ControlResult
    {
        public double Min { get; set; }

        public double Max { get; set; }

        public double Step { get; set; } = 1;

        public double From { get; set; }

        public double To { get; set; }

        public string Text { get; set; }
    }
}
