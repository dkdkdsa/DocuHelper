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
        public const string TitleKind = PptLayoutKinds.Title;

        public const string ContentKind = PptLayoutKinds.Content;

        private const int MaxListedLayouts = 12;

        public static SlideLayoutPart FindLayout(PresentationDocument document, string kind, SlidePart near)
        {
            var masters = Masters(document, near);
            return kind == null ? null : FindByKind(masters, kind);
        }

        public static PptLayoutChoice Resolve(PresentationDocument document, string request, SlidePart near)
        {
            var masters = Masters(document, near);
            if (masters.Count == 0)
                throw new ToolInputException("이 파일에는 슬라이드 마스터가 없어서 레이아웃을 쓸 수 없어요.");

            var text = (request ?? string.Empty).Trim();
            if (text.Length == 0)
                text = PptLayoutKinds.Content;

            var named = FindByName(masters, text);
            if (named != null)
                return new PptLayoutChoice(named, PptLayoutKinds.Classify(named), named.SlideMasterPart);

            var kind = PptLayoutKinds.Parse(text);
            if (kind == null)
            {
                var names = masters.SelectMany(Layouts).Select(LayoutName).Distinct().Take(MaxListedLayouts);
                throw new ToolInputException(
                    $"'{text}' 레이아웃을 찾지 못했어요. {string.Join(", ", PptLayoutKinds.All)} 중 하나나 이 파일의 레이아웃 이름({string.Join(", ", names)})으로 알려 주세요.");
            }

            var found = FindByKind(masters, kind);
            return found != null
                ? new PptLayoutChoice(found, kind, found.SlideMasterPart)
                : new PptLayoutChoice(null, kind, masters[0]);
        }

        public static PptLayoutChoice Existing(SlideLayoutPart layout) =>
            new(layout, PptLayoutKinds.Classify(layout), layout.SlideMasterPart);

        public static void Materialize(PresentationDocument document, IEnumerable<PptLayoutChoice> choices)
        {
            var made = new Dictionary<(SlideMasterPart, string), SlideLayoutPart>();
            foreach (var choice in choices.Where(c => c.IsNew))
            {
                var key = (choice.Master, choice.Kind);
                if (!made.TryGetValue(key, out var layout))
                {
                    layout = PptLayoutTemplates.Create(document, choice.Master, choice.Kind);
                    made[key] = layout;
                }

                choice.Attach(layout);
            }
        }

        public static string LayoutName(SlideLayoutPart layout) =>
            layout?.SlideLayout?.CommonSlideData?.Name?.Value ?? "layout";

        public static PptSlideRef Add(PresentationDocument document, int after, SlideLayoutPart layout, string title, IReadOnlyList<PptLine> lines)
        {
            var presentationPart = document.PresentationPart;
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.AddPart(layout, "rId1");

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

            return PptSlideOrder.Insert(document, slidePart, after);
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

        public static IEnumerable<P.Shape> TextPlaceholders(SlideLayoutPart layout)
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

        public static List<SlideMasterPart> Masters(PresentationDocument document, SlidePart near)
        {
            var presentationPart = document.PresentationPart;
            var masters = new List<SlideMasterPart>();
            var nearMaster = near?.SlideLayoutPart?.SlideMasterPart;
            if (nearMaster != null)
                masters.Add(nearMaster);

            foreach (var masterId in presentationPart.Presentation.SlideMasterIdList?.Elements<P.SlideMasterId>() ?? Enumerable.Empty<P.SlideMasterId>())
            {
                var relationshipId = masterId.RelationshipId?.Value;
                if (!string.IsNullOrEmpty(relationshipId) && presentationPart.TryGetPartById(relationshipId, out var part) && part is SlideMasterPart master && !masters.Contains(master))
                    masters.Add(master);
            }

            masters.AddRange(presentationPart.SlideMasterParts.Where(m => !masters.Contains(m)));
            return masters;
        }

        public static List<SlideLayoutPart> Layouts(SlideMasterPart master)
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

        private static PptShapeRole RoleOf(P.Shape shape) =>
            PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(PptPlaceholder.Of(shape)));

        private static SlideLayoutPart FindByName(List<SlideMasterPart> masters, string name)
        {
            foreach (var master in masters)
            {
                var found = Layouts(master).FirstOrDefault(l => string.Equals(LayoutName(l).Trim(), name, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                    return found;
            }

            return null;
        }

        private static SlideLayoutPart FindByKind(List<SlideMasterPart> masters, string kind)
        {
            foreach (var master in masters)
            {
                var found = Pick(Layouts(master), kind);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static SlideLayoutPart Pick(List<SlideLayoutPart> layouts, string kind)
        {
            var exact = layouts.FirstOrDefault(l => PptLayoutKinds.Classify(l) == kind);
            if (exact != null)
                return exact;

            if (kind == TitleKind)
            {
                return layouts.FirstOrDefault(l => Types(l).Contains("ctrTitle"))
                    ?? layouts.FirstOrDefault(l => Types(l).Contains("subTitle") && HasTitle(l));
            }

            if (kind == ContentKind)
            {
                return layouts.FirstOrDefault(l => TypeOf(l) == P.SlideLayoutValues.Text && HasTitle(l) && HasBody(l))
                    ?? layouts.Where(l => HasTitle(l) && HasBody(l)).OrderBy(l => Types(l).Count(t => t == "body" || t == "obj")).FirstOrDefault();
            }

            return null;
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
