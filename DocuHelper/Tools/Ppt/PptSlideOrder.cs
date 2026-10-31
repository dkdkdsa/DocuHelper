using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptSlideOrder
    {
        public const uint FirstId = 256;

        public const int MaxSlides = 1000;

        private const int MaxRenumberGroups = 6;

        public static uint NextId(P.Presentation presentation)
        {
            var ids = presentation.SlideIdList?.Elements<P.SlideId>().Select(s => s.Id?.Value ?? 0).ToList() ?? new List<uint>();
            return ids.Count == 0 ? FirstId : Math.Max(FirstId, ids.Max() + 1);
        }

        public static PptSlideRef Insert(PresentationDocument document, SlidePart part, int index)
        {
            var presentationPart = document.PresentationPart;
            var presentation = presentationPart.Presentation;
            presentation.SlideIdList ??= new P.SlideIdList();
            var list = presentation.SlideIdList;
            var existing = list.Elements<P.SlideId>().ToList();
            var slideId = new P.SlideId { Id = NextId(presentation), RelationshipId = presentationPart.GetIdOfPart(part) };

            var position = Math.Clamp(index, 0, existing.Count);
            if (position == 0)
                list.PrependChild(slideId);
            else
                existing[position - 1].InsertAfterSelf(slideId);

            PptSections.Insert(presentation, slideId.Id.Value, position == 0 ? null : existing[position - 1].Id?.Value);
            return new PptSlideRef(position + 1, slideId, part);
        }

        public static void Reorder(PresentationDocument document, IReadOnlyList<PptSlideRef> order, IReadOnlyCollection<uint> moved)
        {
            var presentation = document.PresentationPart.Presentation;
            var list = presentation.SlideIdList;
            foreach (var slide in order)
            {
                slide.Element.Remove();
                list.AppendChild(slide.Element);
            }

            for (var i = 0; i < order.Count; i++)
            {
                if (moved.Contains(order[i].SlideId))
                    PptSections.Move(presentation, order[i].SlideId, i == 0 ? null : order[i - 1].SlideId);
            }
        }

        public static int Remove(PresentationDocument document, PptSlideRef slide)
        {
            var presentationPart = document.PresentationPart;
            var presentation = presentationPart.Presentation;
            var removedLinks = RemoveJumpsTo(document, slide.Part);

            PptCustomShows.RemoveSlide(presentationPart, slide.Part);
            PptSections.Remove(presentation, slide.SlideId);
            slide.Element.Remove();
            if (slide.Part.NotesSlidePart != null)
                slide.Part.DeletePart(slide.Part.NotesSlidePart);

            presentationPart.DeletePart(slide.Part);
            return removedLinks;
        }

        public static void FitShowRange(PresentationPart presentationPart, IReadOnlyCollection<int> deleted, int oldCount)
        {
            var show = presentationPart.PresentationPropertiesPart?.PresentationProperties?.GetFirstChild<P.ShowProperties>();
            var range = show?.GetFirstChild<P.SlideRange>();
            if (range == null)
                return;

            var start = (int)Math.Clamp(range.Start != null && range.Start.HasValue ? range.Start.Value : 1, 1, Math.Max(1, oldCount));
            var end = (int)Math.Clamp(range.End != null && range.End.HasValue ? range.End.Value : oldCount, start, Math.Max(start, oldCount));
            var kept = Enumerable.Range(start, end - start + 1).Where(n => !deleted.Contains(n)).ToList();
            if (kept.Count == 0)
            {
                range.InsertBeforeSelf(new P.SlideAll());
                range.Remove();
                return;
            }

            range.Start = (uint)(kept[0] - deleted.Count(d => d < kept[0]));
            range.End = (uint)(kept[^1] - deleted.Count(d => d < kept[^1]));
        }

        public static string Ranges(IEnumerable<int> numbers)
        {
            var sorted = numbers.Distinct().OrderBy(n => n).ToList();
            var parts = new List<string>();
            for (var i = 0; i < sorted.Count;)
            {
                var j = i;
                while (j + 1 < sorted.Count && sorted[j + 1] == sorted[j] + 1)
                    j++;

                parts.Add(j > i
                    ? $"{sorted[i].ToString(CultureInfo.InvariantCulture)}-{sorted[j].ToString(CultureInfo.InvariantCulture)}"
                    : sorted[i].ToString(CultureInfo.InvariantCulture));

                i = j + 1;
            }

            return string.Join(", ", parts);
        }

        public static string Renumbering(IEnumerable<(int Old, int New)> pairs)
        {
            var moved = pairs.Where(p => p.Old != p.New).OrderBy(p => p.Old).ToList();
            if (moved.Count == 0)
                return string.Empty;

            var groups = new List<List<(int Old, int New)>>();
            foreach (var pair in moved)
            {
                var last = groups.Count == 0 ? null : groups[^1];
                if (last != null && last[^1].Old + 1 == pair.Old && last[^1].New + 1 == pair.New)
                    last.Add(pair);
                else
                    groups.Add(new List<(int Old, int New)> { pair });
            }

            if (groups.Count > MaxRenumberGroups)
                return " Many slides were renumbered; call read_slides for the new numbers.";

            var parts = groups.Select(g => g.Count == 1
                ? $"the old slide {g[0].Old.ToString(CultureInfo.InvariantCulture)} is now {g[0].New.ToString(CultureInfo.InvariantCulture)}"
                : $"the old slides {Ranges(g.Select(p => p.Old))} are now {Ranges(g.Select(p => p.New))}");

            var text = string.Join("; ", parts);
            return " " + char.ToUpperInvariant(text[0]) + text.Substring(1) + ".";
        }

        private static int RemoveJumpsTo(PresentationDocument document, SlidePart target)
        {
            var removed = 0;
            foreach (var other in PptSlides.List(document))
            {
                if (ReferenceEquals(other.Part, target))
                    continue;

                var ids = other.Part.Parts
                    .Where(p => ReferenceEquals(p.OpenXmlPart, target))
                    .Select(p => p.RelationshipId)
                    .ToHashSet(StringComparer.Ordinal);

                if (ids.Count == 0)
                    continue;

                var links = other.Part.Slide.Descendants<A.HyperlinkType>()
                    .Where(h => h.Id?.Value != null && ids.Contains(h.Id.Value))
                    .ToList();

                foreach (var link in links)
                {
                    link.Remove();
                    removed++;
                }

                var used = PptPartCopier.ReferencedIds(other.Part.Slide);
                foreach (var id in ids.Where(i => !used.Contains(i)))
                    other.Part.DeletePart(id);
            }

            return removed;
        }
    }
}
