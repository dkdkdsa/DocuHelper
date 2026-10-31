using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public sealed class PptTextTarget
    {
        private readonly PresentationDocument _document;
        private readonly PptSlideRef _slide;

        private PptTextTarget(PresentationDocument document, PptSlideRef slide, OpenXmlCompositeElement element)
        {
            _document = document;
            _slide = slide;
            Element = element;
        }

        public OpenXmlCompositeElement Element { get; }

        public P.Shape Shape => Element as P.Shape;

        public bool IsTable => Element is P.GraphicFrame && PptTargets.KindOf(Element) == PptShapeKind.Table;

        public uint ShapeId => PptTargets.IdOf(Element);

        public static PptTextTarget Of(PresentationDocument document, PptSlideRef slide, OpenXmlCompositeElement element) =>
            new(document, slide, element);

        public static PptTextTarget Resolve(PresentationDocument document, PptSlideRef slide, string key)
        {
            if (uint.TryParse((key ?? string.Empty).Trim().TrimStart('#'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                var found = PptTargets.All(slide.Tree).FirstOrDefault(t => PptTargets.IdOf(t) == id);
                if (found is P.GraphicFrame frame && PptTargets.KindOf(frame) == PptShapeKind.Table)
                    return new PptTextTarget(document, slide, frame);
            }

            return new PptTextTarget(document, slide, PptSlides.Resolve(document, slide, key, false));
        }

        public List<A.Paragraph> Paragraphs()
        {
            if (Element is P.Shape shape)
                return shape.TextBody == null ? new List<A.Paragraph>() : shape.TextBody.Elements<A.Paragraph>().ToList();

            if (!IsTable)
                return new List<A.Paragraph>();

            var table = Element.Descendants<A.Table>().FirstOrDefault();
            return table == null ? new List<A.Paragraph>() : table.Descendants<A.Paragraph>().ToList();
        }

        public int ParagraphCount()
        {
            var count = Paragraphs().Count;
            return Shape != null && count == 0 ? 1 : count;
        }

        public List<A.Paragraph> EnsureParagraphs()
        {
            if (Shape != null)
                PptSlides.EnsureTextBody(Shape);

            return Paragraphs();
        }

        public PptShapeStyle Style()
        {
            var defaults = _document.PresentationPart.Presentation.DefaultTextStyle;
            if (Shape != null)
                return new PptSlideStyles(_slide.Part, defaults).For(Shape);

            var lists = new List<OpenXmlElement>();
            if (defaults != null)
                lists.Add(defaults);

            return new PptShapeStyle("table", lists, new List<A.BodyProperties>(), null);
        }

        public PptShapeInfo Info() =>
            PptReader.ReadSlide(_document, _slide).Shapes.FirstOrDefault(s => s.ShapeId == ShapeId);

        public string Label()
        {
            if (IsTable)
                return $"[{ShapeId.ToString(CultureInfo.InvariantCulture)}] table";

            var info = Info();
            return info == null ? $"[{ShapeId.ToString(CultureInfo.InvariantCulture)}]" : PptOutput.ShapeLabel(info);
        }
    }
}
