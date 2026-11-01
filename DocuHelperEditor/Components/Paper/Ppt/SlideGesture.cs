namespace DocuHelperEditor.Components.Paper.Ppt
{
    public sealed class SlideGesture
    {
        public string Kind { get; set; }

        public string SlideId { get; set; }

        public string Id { get; set; }

        public string Handle { get; set; }

        public double Dx { get; set; }

        public double Dy { get; set; }

        public double Angle { get; set; }

        public bool Additive { get; set; }

        public string Key { get; set; }

        public bool Shift { get; set; }
    }
}
