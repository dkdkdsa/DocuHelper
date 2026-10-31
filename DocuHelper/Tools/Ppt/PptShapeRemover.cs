using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptShapeRemover
    {
        public static IReadOnlyCollection<uint> IdsOf(OpenXmlElement element) =>
            element.Descendants<P.NonVisualDrawingProperties>().Select(p => p.Id?.Value ?? 0).Where(i => i != 0).ToList();

        public static int Remove(PptSlideRef slide, IReadOnlyList<OpenXmlCompositeElement> elements)
        {
            var ids = new HashSet<uint>(elements.SelectMany(IdsOf));
            var referenced = new HashSet<string>(elements.SelectMany(e => PptPartCopier.ReferencedIds(e)));
            foreach (var element in elements)
                element.Remove();

            Disconnect(slide.Tree, ids);
            var effects = PptTimingRefs.RemoveShapes(slide, ids);
            PptLinkWriter.Prune(slide);
            ReleaseParts(slide.Part, referenced);
            return effects;
        }

        private static void Disconnect(OpenXmlElement tree, HashSet<uint> ids)
        {
            foreach (var start in tree.Descendants<A.StartConnection>().Where(s => s.Id?.Value is uint id && ids.Contains(id)).ToList())
                start.Remove();

            foreach (var end in tree.Descendants<A.EndConnection>().Where(s => s.Id?.Value is uint id && ids.Contains(id)).ToList())
                end.Remove();
        }

        private static void ReleaseParts(SlidePart part, HashSet<string> candidates)
        {
            var used = PptPartCopier.ReferencedIds(part.Slide);
            foreach (var id in candidates.Where(c => !used.Contains(c)))
            {
                if (part.TryGetPartById(id, out var target) && target is not (SlidePart or SlideLayoutPart or SlideMasterPart or NotesSlidePart))
                    part.DeletePart(id);
            }

            foreach (var relationship in part.DataPartReferenceRelationships.Where(r => candidates.Contains(r.Id) && !used.Contains(r.Id)).ToList())
                part.DeleteReferenceRelationship(relationship);
        }
    }
}
