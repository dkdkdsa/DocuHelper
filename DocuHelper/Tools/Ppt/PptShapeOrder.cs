using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptShapeOrder
    {
        public static bool Move(P.ShapeTree tree, IReadOnlyList<OpenXmlCompositeElement> selected, string order, Func<OpenXmlElement, PptBox?> visual)
        {
            var before = tree.ChildElements.ToList();
            var ordered = selected.OrderBy(s => PptGrouping.IndexIn(tree, s)).ToList();
            switch (order)
            {
                case "front":
                    foreach (var element in ordered)
                    {
                        element.Remove();
                        PptSlides.AppendToTree(tree, element);
                    }

                    break;
                case "back":
                    var anchor = tree.GetFirstChild<P.GroupShapeProperties>();
                    foreach (var element in Enumerable.Reverse(ordered))
                    {
                        element.Remove();
                        anchor.InsertAfterSelf(element);
                    }

                    break;
                case "forward":
                    for (var i = ordered.Count - 1; i >= 0; i--)
                    {
                        var target = ordered[i].ElementsAfter().FirstOrDefault(e => Candidate(e, selected) && Overlaps(ordered[i], e, visual));
                        if (target != null)
                        {
                            ordered[i].Remove();
                            target.InsertAfterSelf(ordered[i]);
                        }
                    }

                    break;
                default:
                    foreach (var element in ordered)
                    {
                        var target = element.ElementsBefore().LastOrDefault(e => Candidate(e, selected) && Overlaps(element, e, visual));
                        if (target != null)
                        {
                            element.Remove();
                            target.InsertBeforeSelf(element);
                        }
                    }

                    break;
            }

            return !before.SequenceEqual(tree.ChildElements.ToList(), ReferenceEqualityComparer.Instance);
        }

        private static bool Candidate(OpenXmlElement element, IReadOnlyList<OpenXmlCompositeElement> selected) =>
            PptTargets.IsTarget(element) && !selected.Contains(element);

        private static bool Overlaps(OpenXmlElement left, OpenXmlElement right, Func<OpenXmlElement, PptBox?> visual)
        {
            var a = visual(left);
            var b = visual(right);
            return a == null || b == null || a.Value.Intersects(b.Value);
        }
    }
}
