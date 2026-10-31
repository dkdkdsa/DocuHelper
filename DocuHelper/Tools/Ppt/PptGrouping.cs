using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptGrouping
    {
        public static PptBox? Visual(PptSlideRef slide, OpenXmlElement element)
        {
            var box = PptXfrm.Resolve(slide, element);
            if (box == null)
                return null;

            var rotation = PptXfrm.ReadRotation(element);
            return rotation == 0 ? box : box.Value.Rotated(rotation);
        }

        public static void CheckGroupable(PptSlideRef slide, OpenXmlCompositeElement member)
        {
            var label = Label(member);
            if (member is P.Shape shape && PptPlaceholder.Of(shape) != null)
                throw new ToolInputException($"{label}은 제목이나 본문 같은 자리 표시 상자라서 묶을 수 없어요. 글상자나 도형만 묶어 주세요.");

            if (member is P.GraphicFrame && PptTargets.KindOf(member) != PptShapeKind.Chart)
                throw new ToolInputException($"{label}은 표(또는 다른 개체)라서 묶을 수 없어요. 도형, 그림, 차트만 묶을 수 있어요.");

            if (Visual(slide, member) == null)
                throw new ToolInputException($"{label}의 위치와 크기를 알 수 없어서 묶을 수 없어요.");
        }

        public static void CheckUngroupable(OpenXmlCompositeElement element)
        {
            var label = Label(element);
            if (element is not P.GroupShape group)
                throw new ToolInputException($"{label}은 묶음이 아니에요. 묶음(그룹) 번호를 알려 주세요.");

            var transform = group.GroupShapeProperties?.TransformGroup;
            if (PptXfrm.ReadRotation(group) != 0 || PptXfrm.ReadFlipH(group) || PptXfrm.ReadFlipV(group))
                throw new ToolInputException($"{label}은 돌리거나 뒤집은 묶음이라서 풀 수 없어요. 먼저 회전을 0도로 되돌려 주세요.");

            var extents = transform?.Extents;
            var childExtents = transform?.ChildExtents;
            var scaled = (extents?.Cx?.Value ?? 0) != 0 || (extents?.Cy?.Value ?? 0) != 0;
            if (scaled && ((childExtents?.Cx?.Value ?? 0) == 0 || (childExtents?.Cy?.Value ?? 0) == 0))
                throw new ToolInputException($"{label}은 안쪽 크기를 알 수 없는 묶음이라서 풀 수 없어요.");
        }

        public static P.GroupShape Group(PptSlideRef slide, IReadOnlyList<OpenXmlCompositeElement> members)
        {
            var tree = slide.Tree;
            var ordered = members.OrderBy(m => IndexIn(tree, m)).ToList();
            var bounds = PptBox.Bounds(ordered.Select(m => Visual(slide, m).Value));
            var id = PptSlides.NextShapeId(tree);
            var group = new P.GroupShape(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = "그룹 " + (id - 1).ToString(CultureInfo.InvariantCulture) },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(
                    new A.TransformGroup(
                        new A.Offset { X = bounds.X, Y = bounds.Y },
                        new A.Extents { Cx = bounds.Width, Cy = bounds.Height },
                        new A.ChildOffset { X = bounds.X, Y = bounds.Y },
                        new A.ChildExtents { Cx = bounds.Width, Cy = bounds.Height })));

            ordered[^1].InsertBeforeSelf(group);
            foreach (var member in ordered)
            {
                member.Remove();
                group.AppendChild(member);
            }

            return group;
        }

        public static List<OpenXmlCompositeElement> Ungroup(P.GroupShape group)
        {
            var transform = group.GroupShapeProperties?.TransformGroup;
            var childExtents = transform?.ChildExtents;
            var extents = transform?.Extents;
            var scaleX = ScaleOf(extents?.Cx?.Value, childExtents?.Cx?.Value);
            var scaleY = ScaleOf(extents?.Cy?.Value, childExtents?.Cy?.Value);
            var originX = transform?.Offset?.X?.Value ?? 0;
            var originY = transform?.Offset?.Y?.Value ?? 0;
            var innerX = transform?.ChildOffset?.X?.Value ?? 0;
            var innerY = transform?.ChildOffset?.Y?.Value ?? 0;

            var children = group.ChildElements.Where(c => c is not P.NonVisualGroupShapeProperties and not P.GroupShapeProperties).ToList();
            var result = new List<OpenXmlCompositeElement>();
            foreach (var child in children)
            {
                if (child is OpenXmlCompositeElement target && PptTargets.IsTarget(child))
                {
                    var box = PptXfrm.Read(target);
                    if (box != null)
                    {
                        var current = box.Value;
                        PptXfrm.Write(target, new PptBox(
                            originX + (long)Math.Round((current.X - innerX) * scaleX),
                            originY + (long)Math.Round((current.Y - innerY) * scaleY),
                            (long)Math.Round(current.Width * scaleX),
                            (long)Math.Round(current.Height * scaleY)));
                    }

                    result.Add(target);
                }

                child.Remove();
                group.InsertBeforeSelf(child);
            }

            group.Remove();
            return result;
        }

        public static int IndexIn(OpenXmlElement tree, OpenXmlElement element)
        {
            var index = 0;
            foreach (var child in tree.ChildElements)
            {
                if (ReferenceEquals(child, element))
                    return index;

                index++;
            }

            return -1;
        }

        private static double ScaleOf(long? outer, long? inner) =>
            inner == null || inner.Value == 0 ? 1 : (outer ?? 0) / (double)inner.Value;

        private static string Label(OpenXmlElement element) =>
            $"{PptTargets.IdOf(element).ToString(CultureInfo.InvariantCulture)}번({PptTargets.KoreanName(element)})";
    }
}
