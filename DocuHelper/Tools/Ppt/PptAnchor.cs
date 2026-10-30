using System.Globalization;

namespace DocuHelper.Tools.Ppt
{
    public static class PptAnchor
    {
        public static string Slide(uint slideId) => "s:" + slideId.ToString(CultureInfo.InvariantCulture);

        public static string Shape(uint slideId, uint shapeId) => Slide(slideId) + "/" + shapeId.ToString(CultureInfo.InvariantCulture);

        public static string Notes(uint slideId) => Slide(slideId) + "/notes";
    }
}
