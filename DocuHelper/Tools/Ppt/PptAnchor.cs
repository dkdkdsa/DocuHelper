using System.Globalization;

namespace DocuHelper.Tools.Ppt
{
    public static class PptAnchor
    {
        public static string Slide(uint slideId) => "s:" + slideId.ToString(CultureInfo.InvariantCulture);

        public static string Shape(uint slideId, uint shapeId) => Slide(slideId) + "/" + shapeId.ToString(CultureInfo.InvariantCulture);

        public static string Notes(uint slideId) => Slide(slideId) + "/notes";

        public static string Comment(uint slideId, int index) => Slide(slideId) + "/c" + index.ToString(CultureInfo.InvariantCulture);

        public static string Cell(uint slideId, uint shapeId, int row, int column) =>
            Shape(slideId, shapeId) + "#r" + row.ToString(CultureInfo.InvariantCulture) + "c" + column.ToString(CultureInfo.InvariantCulture);
    }
}
