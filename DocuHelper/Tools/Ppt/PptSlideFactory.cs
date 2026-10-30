using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptSlideFactory
    {
        public const string TitleKind = "title";

        public const string ContentKind = "title_content";

        private const uint FirstSlideId = 256;

        public static SlideLayoutPart FindLayout(PresentationDocument document, string kind, SlidePart near)
        {
            var masters = new List<SlideMasterPart>();
            var nearMaster = near?.SlideLayoutPart?.SlideMasterPart;
            if (nearMaster != null)
                masters.Add(nearMaster);

            masters.AddRange(document.PresentationPart.SlideMasterParts.Where(m => m != nearMaster));
            foreach (var master in masters)
            {
                var found = Pick(Layouts(master), kind);
                if (found != null)
                    return found;
            }

            return null;
        }

        public static string LayoutName(SlideLayoutPart layout) =>
            layout?.SlideLayout?.CommonSlideData?.Name?.Value ?? "layout";

        public static PptSlideRef Add(PresentationDocument document, int after, SlideLayoutPart layout, string title, IReadOnlyList<PptLine> lines)
        {
            var presentationPart = document.PresentationPart;
            var presentation = presentationPart.Presentation;
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.AddPart(layout);

            var tree = new P.ShapeTree(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(
                    new A.TransformGroup(
                        new A.Offset { X = 0L, Y = 0L },
                        new A.Extents { Cx = 0L, Cy = 0L },
                        new A.ChildOffset { X = 0L, Y = 0L },
                        new A.ChildExtents { Cx = 0L, Cy = 0L })));

            var nextId = 2U;
            var created = new List<P.Shape>();
            foreach (var layoutShape in TextPlaceholders(layout))
            {
                var shape = PlaceholderFrom(layoutShape, nextId++);
                tree.AppendChild(shape);
                created.Add(shape);
            }

            slidePart.Slide = new P.Slide(
                new P.CommonSlideData(tree),
                new P.ColorMapOverride(new A.MasterColorMapping()));

            var titleShape = created.FirstOrDefault(s => RoleOf(s) == PptShapeRole.Title);
            if (titleShape != null && !string.IsNullOrEmpty(title))
                PptText.SetLines(titleShape.TextBody, new[] { new PptLine(title, null) });

            var textShape = created.FirstOrDefault(s => RoleOf(s) == PptShapeRole.Body) ?? created.FirstOrDefault(s => RoleOf(s) == PptShapeRole.Subtitle);
            if (textShape != null && lines != null && lines.Count > 0)
                PptText.SetLines(textShape.TextBody, lines);

            presentation.SlideIdList ??= new P.SlideIdList();
            var list = presentation.SlideIdList;
            var existing = list.Elements<P.SlideId>().ToList();
            var id = existing.Count == 0 ? FirstSlideId : Math.Max(FirstSlideId, existing.Max(s => s.Id?.Value ?? 0) + 1);
            var slideId = new P.SlideId { Id = id, RelationshipId = presentationPart.GetIdOfPart(slidePart) };

            var position = Math.Clamp(after, 0, existing.Count);
            if (position == 0)
                list.PrependChild(slideId);
            else
                existing[position - 1].InsertAfterSelf(slideId);

            return new PptSlideRef(position + 1, slideId, slidePart);
        }

        public static P.Shape PlaceholderFrom(P.Shape layoutShape, uint id)
        {
            var placeholder = (P.PlaceholderShape)PptPlaceholder.Of(layoutShape).CloneNode(true);
            placeholder.HasCustomPrompt = null;
            var name = layoutShape.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value;
            return new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = string.IsNullOrEmpty(name) ? $"Placeholder {id - 1}" : name },
                    new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                    new P.ApplicationNonVisualDrawingProperties(placeholder)),
                new P.ShapeProperties(),
                new P.TextBody(new A.BodyProperties(), new A.ListStyle(), PptText.EmptyParagraph()));
        }

        private static PptShapeRole RoleOf(P.Shape shape) =>
            PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(PptPlaceholder.Of(shape)));

        private static IEnumerable<P.Shape> TextPlaceholders(SlideLayoutPart layout)
        {
            var tree = layout.SlideLayout?.CommonSlideData?.ShapeTree;
            if (tree == null)
                return Enumerable.Empty<P.Shape>();

            return tree.Elements<P.Shape>().Where(s =>
            {
                var placeholder = PptPlaceholder.Of(s);
                return placeholder != null && RoleOf(s) is PptShapeRole.Title or PptShapeRole.Subtitle or PptShapeRole.Body;
            });
        }

        private static List<SlideLayoutPart> Layouts(SlideMasterPart master)
        {
            var layouts = new List<SlideLayoutPart>();
            var ids = master.SlideMaster?.SlideLayoutIdList;
            if (ids != null)
            {
                foreach (var layoutId in ids.Elements<P.SlideLayoutId>())
                {
                    var relationshipId = layoutId.RelationshipId?.Value;
                    if (!string.IsNullOrEmpty(relationshipId) && master.TryGetPartById(relationshipId, out var part) && part is SlideLayoutPart layout && !layouts.Contains(layout))
                        layouts.Add(layout);
                }
            }

            layouts.AddRange(master.SlideLayoutParts.Where(l => !layouts.Contains(l)));
            return layouts;
        }

        private static SlideLayoutPart Pick(List<SlideLayoutPart> layouts, string kind)
        {
            if (kind == TitleKind)
            {
                return layouts.FirstOrDefault(l => TypeOf(l) == P.SlideLayoutValues.Title)
                    ?? layouts.FirstOrDefault(l => Types(l).Contains("ctrTitle"))
                    ?? layouts.FirstOrDefault(l => Types(l).Contains("subTitle") && HasTitle(l));
            }

            return layouts.FirstOrDefault(l => TypeOf(l) == P.SlideLayoutValues.Object && HasTitle(l) && HasBody(l))
                ?? layouts.FirstOrDefault(l => TypeOf(l) == P.SlideLayoutValues.Text && HasTitle(l) && HasBody(l))
                ?? layouts.Where(l => HasTitle(l) && HasBody(l)).OrderBy(l => Types(l).Count(t => t == "body" || t == "obj")).FirstOrDefault();
        }

        private static P.SlideLayoutValues? TypeOf(SlideLayoutPart layout)
        {
            var type = layout.SlideLayout?.Type;
            return type != null && type.HasValue ? type.Value : null;
        }

        private static List<string> Types(SlideLayoutPart layout) =>
            TextPlaceholders(layout).Select(s => PptPlaceholder.TypeOf(PptPlaceholder.Of(s))).ToList();

        private static bool HasTitle(SlideLayoutPart layout) => Types(layout).Any(t => t == "title" || t == "ctrTitle");

        private static bool HasBody(SlideLayoutPart layout) => Types(layout).Any(t => t == "body" || t == "obj");
    }
}
