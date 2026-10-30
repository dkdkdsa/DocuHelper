using DocumentFormat.OpenXml.Packaging;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public class PptSlideRef
    {
        public PptSlideRef(int number, P.SlideId element, SlidePart part)
        {
            Number = number;
            Element = element;
            Part = part;
        }

        public int Number { get; }

        public P.SlideId Element { get; }

        public SlidePart Part { get; }

        public uint SlideId => Element.Id?.Value ?? 0;

        public P.ShapeTree Tree => Part.Slide?.CommonSlideData?.ShapeTree;
    }
}
