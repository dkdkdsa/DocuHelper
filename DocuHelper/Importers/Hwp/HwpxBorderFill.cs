using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed class HwpxBorderFill
    {
        public HwpBorderLine Top { get; private set; }

        public HwpBorderLine Left { get; private set; }

        public HwpBorderLine Bottom { get; private set; }

        public HwpBorderLine Right { get; private set; }

        public string FaceColor { get; private set; }

        public static HwpxBorderFill Read(XElement element)
        {
            var fill = new HwpxBorderFill
            {
                Top = Line(element, "topBorder"),
                Left = Line(element, "leftBorder"),
                Bottom = Line(element, "bottomBorder"),
                Right = Line(element, "rightBorder"),
            };
            var face = element.Descendants().FirstOrDefault(e => e.Name.LocalName == "winBrush")?.Attribute("faceColor")?.Value;
            fill.FaceColor = string.IsNullOrEmpty(face) ? null : face.TrimStart('#');
            return fill;
        }

        private static HwpBorderLine Line(XElement element, string name)
        {
            var side = element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
            var type = side?.Attribute("type")?.Value;
            if (string.IsNullOrEmpty(type) || type == "NONE")
                return null;

            var width = side.Attribute("width")?.Value ?? "0.12 mm";
            var mm = double.TryParse(width.Replace("mm", string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.12;
            return new HwpBorderLine
            {
                Type = type,
                WidthMm = mm,
                Color = side.Attribute("color")?.Value ?? "#000000",
            };
        }
    }
}
