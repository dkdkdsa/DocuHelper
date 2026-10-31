using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptFooters
    {
        public const string Footer = "ftr";

        public const string SlideNumber = "sldNum";

        public const string Date = "dt";

        public static readonly IReadOnlyList<string> Kinds = new[] { Footer, SlideNumber, Date };

        public static string TypeOf(P.Shape shape) =>
            TokenOf(shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape);

        public static string TokenOf(OpenXmlElement element) =>
            element == null ? null : element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "type").Value;

        public static IReadOnlyList<P.Shape> Of(P.ShapeTree tree, string kind) =>
            tree == null ? new List<P.Shape>() : tree.Elements<P.Shape>().Where(s => TypeOf(s) == kind).ToList();

        public static P.PlaceholderShape LayoutPlaceholder(P.ShapeTree layoutTree, string kind) =>
            Of(layoutTree, kind).FirstOrDefault()?.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;
    }
}
