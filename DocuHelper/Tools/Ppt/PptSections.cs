using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using Pm = DocumentFormat.OpenXml.Office2010.PowerPoint;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptSections
    {
        public const string ExtensionUri = "{521415D9-36F7-43E2-AB2F-B90AF26B5E84}";

        public static Pm.SectionList Find(P.Presentation presentation) =>
            presentation?.PresentationExtensionList?
                .Elements<P.PresentationExtension>()
                .SelectMany(e => e.Elements<Pm.SectionList>())
                .FirstOrDefault();

        public static bool Has(P.Presentation presentation) => Find(presentation) != null;

        public static List<PptSectionInfo> Read(P.Presentation presentation)
        {
            var result = new List<PptSectionInfo>();
            var list = Find(presentation);
            if (list == null)
                return result;

            foreach (var section in list.Elements<Pm.Section>())
            {
                var info = new PptSectionInfo { Name = section.Name?.Value ?? string.Empty, Id = section.Id?.Value };
                foreach (var entry in EntriesOf(section))
                {
                    if (entry.Id?.Value is uint id)
                        info.SlideIds.Add(id);
                }

                result.Add(info);
            }

            return result;
        }

        public static void Insert(P.Presentation presentation, uint slideId, uint? afterSlideId)
        {
            var sections = Find(presentation)?.Elements<Pm.Section>().ToList();
            if (sections == null || sections.Count == 0)
                return;

            RemoveEntries(sections, slideId);
            var entry = new Pm.SectionSlideIdListEntry { Id = slideId };
            if (afterSlideId == null)
            {
                Ensure(sections[0]).PrependChild(entry);
                return;
            }

            foreach (var section in sections)
            {
                var anchor = EntriesOf(section).FirstOrDefault(e => e.Id?.Value == afterSlideId.Value);
                if (anchor == null)
                    continue;

                anchor.InsertAfterSelf(entry);
                return;
            }

            Ensure(sections[^1]).AppendChild(entry);
        }

        public static void Remove(P.Presentation presentation, uint slideId)
        {
            var sections = Find(presentation)?.Elements<Pm.Section>().ToList();
            if (sections == null)
                return;

            RemoveEntries(sections, slideId);
        }

        public static void Move(P.Presentation presentation, uint slideId, uint? afterSlideId) =>
            Insert(presentation, slideId, afterSlideId);

        public static void Reconcile(P.Presentation presentation)
        {
            var sections = Find(presentation)?.Elements<Pm.Section>().ToList();
            if (sections == null || sections.Count == 0)
                return;

            var known = new Dictionary<uint, int>();
            for (var i = 0; i < sections.Count; i++)
            {
                foreach (var entry in EntriesOf(sections[i]))
                {
                    if (entry.Id?.Value is uint id)
                        known.TryAdd(id, i);
                }
            }

            var order = (presentation.SlideIdList?.Elements<P.SlideId>() ?? Enumerable.Empty<P.SlideId>())
                .Select(s => s.Id?.Value ?? 0)
                .Where(id => id != 0)
                .ToList();

            var values = order.Select(id => known.TryGetValue(id, out var index) ? index : -1).ToList();
            var keep = LongestNonDecreasing(values);
            var assignment = new List<(uint Id, int Section)>();
            var current = 0;
            for (var i = 0; i < order.Count; i++)
            {
                if (keep.Contains(i))
                    current = values[i];

                assignment.Add((order[i], current));
            }

            for (var i = 0; i < sections.Count; i++)
            {
                var list = Ensure(sections[i]);
                list.RemoveAllChildren();
                foreach (var item in assignment.Where(a => a.Section == i))
                    list.AppendChild(new Pm.SectionSlideIdListEntry { Id = item.Id });
            }
        }

        private static HashSet<int> LongestNonDecreasing(List<int> values)
        {
            var length = new int[values.Count];
            var previous = new int[values.Count];
            var best = -1;
            for (var i = 0; i < values.Count; i++)
            {
                length[i] = values[i] < 0 ? 0 : 1;
                previous[i] = -1;
                if (values[i] < 0)
                    continue;

                for (var j = 0; j < i; j++)
                {
                    if (values[j] >= 0 && values[j] <= values[i] && length[j] + 1 > length[i])
                    {
                        length[i] = length[j] + 1;
                        previous[i] = j;
                    }
                }

                if (best < 0 || length[i] > length[best])
                    best = i;
            }

            var keep = new HashSet<int>();
            for (var i = best; i >= 0; i = previous[i])
                keep.Add(i);

            return keep;
        }

        private static Pm.SectionSlideIdList Ensure(Pm.Section section)
        {
            if (section.SectionSlideIdList != null)
                return section.SectionSlideIdList;

            var list = new Pm.SectionSlideIdList();
            section.AppendChild(list);
            return list;
        }

        private static IEnumerable<Pm.SectionSlideIdListEntry> EntriesOf(Pm.Section section) =>
            section.SectionSlideIdList?.Elements<Pm.SectionSlideIdListEntry>() ?? Enumerable.Empty<Pm.SectionSlideIdListEntry>();

        private static void RemoveEntries(IEnumerable<Pm.Section> sections, uint slideId)
        {
            foreach (var section in sections)
            {
                foreach (var entry in EntriesOf(section).Where(e => e.Id?.Value == slideId).ToList())
                    entry.Remove();
            }
        }
    }
}
