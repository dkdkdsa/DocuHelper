using System.Text.Json.Serialization;

namespace DocuHelper.Dash.Elements
{
    public sealed class PageElement : CanvasElement
    {
        public const double PixelsPerCm = 96 / 2.54;

        public string Paper { get; set; } = "a4l";

        public double Width { get; set; } = 1123;

        public double Height { get; set; } = 794;

        [JsonIgnore]
        public double WidthCm => Width / PixelsPerCm;

        [JsonIgnore]
        public double HeightCm => Height / PixelsPerCm;

        public static PageElement A4Landscape() => new()
        {
            Id = "page",
            Paper = "a4l",
            Width = 1123,
            Height = 794,
            Style = new DashStyle { Fill = "#FFFFFF" },
        };
    }
}
