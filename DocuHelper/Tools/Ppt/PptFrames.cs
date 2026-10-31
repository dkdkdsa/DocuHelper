using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptFrames
    {
        public const long MarginSide = 540000;

        public const long GapBelowTitle = 288000;

        public const long MarginBottom = 540000;

        public static PptBox DeckOf(PresentationDocument document)
        {
            var size = document.PresentationPart.Presentation.SlideSize;
            return new PptBox(0, 0, size?.Cx?.Value ?? 12192000L, size?.Cy?.Value ?? 6858000L);
        }

        public static P.Shape EmptyPlaceholder(PptSlideRef slide, params string[] extraTypes)
        {
            foreach (var shape in slide.Tree.Elements<P.Shape>())
            {
                var placeholder = PptPlaceholder.Of(shape);
                if (placeholder == null)
                    continue;

                var type = PptPlaceholder.TypeOf(placeholder);
                if (PptPlaceholder.RoleOf(type) != PptShapeRole.Body && !extraTypes.Contains(type))
                    continue;

                if (shape.TextBody != null && shape.TextBody.Elements<A.Paragraph>().Any(p => PptText.TextOf(p).Length > 0))
                    continue;

                if (PptXfrm.Resolve(slide, shape) is { IsEmpty: false })
                    return shape;
            }

            return null;
        }

        public static PptBox Content(PptSlideRef slide, PptBox deck)
        {
            var top = MarginSide;
            var title = slide.Tree.Elements<P.Shape>()
                .Where(s => PptPlaceholder.Of(s) != null && PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(PptPlaceholder.Of(s))) == PptShapeRole.Title)
                .Select(s => PptXfrm.Resolve(slide, s))
                .FirstOrDefault(b => b != null);

            if (title != null)
                top = title.Value.Bottom + GapBelowTitle;

            var area = new PptBox(MarginSide, top, deck.Width - 2 * MarginSide, deck.Height - MarginBottom - top);
            if (area.Width < 2 * PptUnits.EmuPerCm || area.Height < 2 * PptUnits.EmuPerCm)
                return deck.Inset(MarginSide, MarginSide, MarginSide, MarginBottom);

            return area;
        }

        public static PptBox Area(PptSlideRef slide, PptBox deck, P.Shape empty) =>
            empty != null ? PptXfrm.Resolve(slide, empty).Value : Content(slide, deck);

        public static void Place(PptSlideRef slide, P.ShapeTree tree, P.Shape empty, OpenXmlElement element)
        {
            if (empty == null)
            {
                PptSlides.AppendToTree(tree, element);
                return;
            }

            PptTimingRefs.RemoveShapes(slide, PptShapeRemover.IdsOf(empty));
            empty.InsertBeforeSelf(element);
            empty.Remove();
        }

        public static int Free(P.ShapeTree tree, P.Shape empty) =>
            PptTargets.All(tree).Count() - (empty == null ? 0 : 1);
    }
}
