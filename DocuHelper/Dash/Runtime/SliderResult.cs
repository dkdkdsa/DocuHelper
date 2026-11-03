namespace DocuHelper.Dash.Runtime
{
    public sealed class SliderResult : ControlResult
    {
        public double Min { get; set; }

        public double Max { get; set; }

        public double Step { get; set; } = 1;

        public double Value { get; set; }

        public string ValueText { get; set; }

        public bool ShowValue { get; set; }
    }
}
