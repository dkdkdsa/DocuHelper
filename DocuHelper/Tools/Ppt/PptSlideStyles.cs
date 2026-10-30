using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class PptSlideStyles
    {
        private readonly List<P.Shape> _layoutShapes;
        private readonly List<P.Shape> _masterShapes;
        private readonly P.TextStyles _textStyles;
        private readonly P.DefaultTextStyle _defaults;

        public PptSlideStyles(SlidePart slidePart, P.DefaultTextStyle defaults)
            : this(slidePart?.SlideLayoutPart, defaults)
        {
        }

        public PptSlideStyles(SlideLayoutPart layoutPart, P.DefaultTextStyle defaults)
        {
            var masterPart = layoutPart?.SlideMasterPart;
            _layoutShapes = Placeholders(layoutPart?.SlideLayout?.CommonSlideData?.ShapeTree);
            _masterShapes = Placeholders(masterPart?.SlideMaster?.CommonSlideData?.ShapeTree);
            _textStyles = masterPart?.SlideMaster?.TextStyles;
            _defaults = defaults;
        }

        public IReadOnlyList<P.Shape> LayoutPlaceholders => _layoutShapes;

        public P.Shape FindLayoutPlaceholder(P.PlaceholderShape placeholder)
        {
            if (placeholder == null)
                return null;

            var index = PptPlaceholder.IndexOf(placeholder);
            var type = PptPlaceholder.TypeOf(placeholder);
            if (index > 0)
            {
                var byIndex = _layoutShapes.FirstOrDefault(s => PptPlaceholder.IndexOf(PptPlaceholder.Of(s)) == index);
                if (byIndex != null)
                    return byIndex;
            }

            var byType = _layoutShapes.FirstOrDefault(s => PptPlaceholder.TypeOf(PptPlaceholder.Of(s)) == type);
            if (byType != null || PptPlaceholder.Family(type) != "title")
                return byType;

            return _layoutShapes.FirstOrDefault(s => PptPlaceholder.Family(PptPlaceholder.TypeOf(PptPlaceholder.Of(s))) == "title");
        }

        public P.Shape FindMasterPlaceholder(string type)
        {
            var family = PptPlaceholder.Family(type);
            return family == null
                ? null
                : _masterShapes.FirstOrDefault(s => PptPlaceholder.Family(PptPlaceholder.TypeOf(PptPlaceholder.Of(s))) == family);
        }

        public PptShapeStyle For(P.Shape shape)
        {
            var placeholder = PptPlaceholder.Of(shape);
            var lists = new List<OpenXmlElement>();
            var bodies = new List<A.BodyProperties>();
            var transforms = new List<A.Transform2D>();
            Collect(shape, lists, bodies, transforms);

            string family;
            if (placeholder == null)
            {
                family = "text";
                lists.Add(_defaults);
                lists.Add(_textStyles?.OtherStyle);
            }
            else
            {
                var layoutShape = FindLayoutPlaceholder(placeholder);
                Collect(layoutShape, lists, bodies, transforms);

                var type = PptPlaceholder.TypeOf(layoutShape != null ? PptPlaceholder.Of(layoutShape) : placeholder);
                family = PptPlaceholder.Family(type);
                Collect(FindMasterPlaceholder(type), lists, bodies, transforms);

                lists.Add(family switch
                {
                    "title" => _textStyles?.TitleStyle,
                    "body" => _textStyles?.BodyStyle,
                    _ => _textStyles?.OtherStyle,
                });
                lists.Add(_defaults);
            }

            var transform = transforms.FirstOrDefault(t => t?.Extents?.Cx != null && t.Extents.Cy != null);
            return new PptShapeStyle(family, lists.Where(l => l != null).ToList(), bodies, transform);
        }

        private static void Collect(P.Shape shape, List<OpenXmlElement> lists, List<A.BodyProperties> bodies, List<A.Transform2D> transforms)
        {
            if (shape == null)
                return;

            lists.Add(shape.TextBody?.ListStyle);
            var body = shape.TextBody?.BodyProperties;
            if (body != null)
                bodies.Add(body);

            transforms.Add(shape.ShapeProperties?.Transform2D);
        }

        private static List<P.Shape> Placeholders(P.ShapeTree tree) =>
            tree == null
                ? new List<P.Shape>()
                : tree.Elements<P.Shape>().Where(s => PptPlaceholder.Of(s) != null).ToList();
    }
}
