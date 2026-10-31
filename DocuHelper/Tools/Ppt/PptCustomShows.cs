using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptCustomShows
    {
        public static List<PptCustomShowInfo> Read(PresentationPart presentationPart)
        {
            var result = new List<PptCustomShowInfo>();
            var shows = presentationPart?.Presentation?.CustomShowList;
            if (shows == null)
                return result;

            var slideIds = new Dictionary<string, uint>();
            foreach (var slide in presentationPart.Presentation.SlideIdList?.Elements<P.SlideId>() ?? Enumerable.Empty<P.SlideId>())
            {
                if (slide.RelationshipId?.Value is string relationship && slide.Id?.Value is uint id)
                    slideIds[relationship] = id;
            }

            foreach (var show in shows.Elements<P.CustomShow>())
            {
                var info = new PptCustomShowInfo { Name = show.Name?.Value ?? string.Empty };
                foreach (var entry in show.SlideList?.Elements<P.SlideListEntry>() ?? Enumerable.Empty<P.SlideListEntry>())
                {
                    if (entry.Id?.Value is string relationship && slideIds.TryGetValue(relationship, out var id))
                        info.SlideIds.Add(id);
                }

                result.Add(info);
            }

            return result;
        }

        public static int RemoveSlide(PresentationPart presentationPart, SlidePart slidePart) =>
            RemoveSlide(presentationPart, presentationPart.GetIdOfPart(slidePart));

        public static int RemoveSlide(PresentationPart presentationPart, string relationshipId)
        {
            var shows = presentationPart?.Presentation?.CustomShowList;
            if (shows == null || string.IsNullOrEmpty(relationshipId))
                return 0;

            var removed = 0;
            var gone = new List<uint>();
            foreach (var show in shows.Elements<P.CustomShow>().ToList())
            {
                var entries = show.SlideList?.Elements<P.SlideListEntry>().Where(e => e.Id?.Value == relationshipId).ToList();
                if (entries == null || entries.Count == 0)
                    continue;

                foreach (var entry in entries)
                {
                    entry.Remove();
                    removed++;
                }

                if (!show.SlideList.Elements<P.SlideListEntry>().Any())
                {
                    if (show.Id?.Value is uint id)
                        gone.Add(id);

                    show.Remove();
                }
            }

            if (!shows.Elements<P.CustomShow>().Any())
                shows.Remove();

            if (gone.Count > 0)
                ResetShowRange(presentationPart, gone);

            return removed;
        }

        private static void ResetShowRange(PresentationPart presentationPart, List<uint> gone)
        {
            var showProperties = presentationPart.PresentationPropertiesPart?.PresentationProperties?.GetFirstChild<P.ShowProperties>();
            var reference = showProperties?.GetFirstChild<P.CustomShowReference>();
            if (reference?.Id?.Value is not uint id || !gone.Contains(id))
                return;

            reference.InsertBeforeSelf(new P.SlideAll());
            reference.Remove();
        }
    }
}
