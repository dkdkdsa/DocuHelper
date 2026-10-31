using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptTimingRefs
    {
        public static int RemoveShapes(PptSlideRef slide, IEnumerable<uint> shapeIds) =>
            RemoveShapes(slide.Part.Slide, shapeIds);

        public static int RemoveShapes(P.Slide slide, IEnumerable<uint> shapeIds)
        {
            var timing = slide?.Timing;
            if (timing == null)
                return 0;

            var ids = new HashSet<string>(shapeIds.Select(i => i.ToString(CultureInfo.InvariantCulture)));
            if (ids.Count == 0)
                return 0;

            var removed = 0;
            foreach (var target in timing.Descendants<P.ShapeTarget>().Where(t => t.ShapeId?.Value != null && ids.Contains(t.ShapeId.Value)).ToList())
            {
                if (target.Parent == null || !IsAttached(target, timing))
                    continue;

                var owner = OwnerOf(target);
                if (owner == null)
                    continue;

                owner.Remove();
                removed++;
            }

            var builds = timing.BuildList;
            if (builds != null)
            {
                foreach (var build in builds.ChildElements.Where(c => BuildShape(c) is string id && ids.Contains(id)).ToList())
                {
                    build.Remove();
                    removed++;
                }

                if (!builds.HasChildren)
                    builds.Remove();
            }

            if (removed > 0)
                Prune(slide, timing);

            return removed;
        }

        private static bool IsAttached(OpenXmlElement element, OpenXmlElement root)
        {
            for (var current = element; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, root))
                    return true;
            }

            return false;
        }

        private static OpenXmlElement OwnerOf(P.ShapeTarget target)
        {
            foreach (var ancestor in target.Ancestors<OpenXmlElement>())
            {
                switch (ancestor)
                {
                    case P.CommonTimeNode node when IsEffect(node):
                        return node.Parent;
                    case P.Video or P.Audio:
                        return ancestor;
                    case P.SequenceTimeNode sequence when sequence.CommonTimeNode?.NodeType?.Value == P.TimeNodeValues.InteractiveSequence:
                        return sequence;
                    case P.Timing:
                        return null;
                }
            }

            return null;
        }

        private static bool IsEffect(P.CommonTimeNode node)
        {
            if (node.PresetId != null)
                return true;

            var type = node.NodeType?.Value;
            return type == P.TimeNodeValues.ClickEffect || type == P.TimeNodeValues.WithEffect || type == P.TimeNodeValues.AfterEffect;
        }

        private static string BuildShape(OpenXmlElement build) => build switch
        {
            P.BuildParagraph paragraph => paragraph.ShapeId?.Value,
            P.BuildGraphics graphics => graphics.ShapeId?.Value,
            P.BuildOleChart chart => chart.ShapeId?.Value,
            P.BuildDiagram diagram => diagram.ShapeId?.Value,
            _ => null,
        };

        private static void Prune(P.Slide slide, P.Timing timing)
        {
            bool changed;
            do
            {
                changed = false;
                foreach (var par in timing.Descendants<P.ParallelTimeNode>().ToList())
                {
                    var node = par.CommonTimeNode;
                    if (node == null || node.NodeType?.Value == P.TimeNodeValues.TmingRoot || IsEffect(node))
                        continue;

                    if (node.ChildTimeNodeList?.HasChildren == true)
                        continue;

                    par.Remove();
                    changed = true;
                }
            }
            while (changed);

            foreach (var sequence in timing.Descendants<P.SequenceTimeNode>().ToList())
            {
                var node = sequence.CommonTimeNode;
                if (node?.ChildTimeNodeList?.HasChildren == true)
                    continue;

                if (node?.NodeType?.Value == P.TimeNodeValues.InteractiveSequence)
                    sequence.Remove();
                else
                    node?.ChildTimeNodeList?.Remove();
            }

            var targeted = new HashSet<string>(timing.Descendants<P.ShapeTarget>().Select(t => t.ShapeId?.Value).Where(v => v != null));
            var builds = timing.BuildList;
            if (builds != null)
            {
                foreach (var build in builds.ChildElements.Where(c => BuildShape(c) is string id && !targeted.Contains(id)).ToList())
                    build.Remove();

                if (!builds.HasChildren)
                    builds.Remove();
            }

            var hasContent = timing.Descendants<P.ShapeTarget>().Any()
                || timing.Descendants<P.Video>().Any()
                || timing.Descendants<P.Audio>().Any()
                || timing.Descendants<P.Command>().Any();

            if (!hasContent)
                slide.Timing = null;
        }
    }
}
