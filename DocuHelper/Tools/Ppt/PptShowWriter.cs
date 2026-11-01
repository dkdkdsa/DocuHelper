using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocuHelper.Tools;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public sealed class PptShowRequest
    {
        public string ShowType { get; set; }

        public bool? Loop { get; set; }

        public bool? UseTimings { get; set; }

        public bool? ShowAnimation { get; set; }

        public bool? ShowNarration { get; set; }

        public bool? MediaControls { get; set; }

        public string RangeType { get; set; }

        public int? From { get; set; }

        public int? To { get; set; }

        public string CustomShowName { get; set; }

        public uint? CustomShowId { get; set; }
    }

    public static class PptShowWriter
    {
        public const string MediaControlsUri = "{2FDB2607-1784-4EEB-B798-7EB5836EED8A}";

        public const string Namespaces = "xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p14=\"http://schemas.microsoft.com/office/powerpoint/2010/main\"";

        public static void Apply(PresentationPart presentation, PptShowRequest request)
        {
            var properties = presentation.PresentationPropertiesPart ?? presentation.AddNewPart<PresentationPropertiesPart>();
            properties.PresentationProperties ??= new P.PresentationProperties();
            ApplyShowProperties(properties.PresentationProperties, request);
        }

        public static void Replace(PresentationPart presentation, IReadOnlyList<(string Name, IReadOnlyList<string> RelationshipIds)> shows, IReadOnlyCollection<string> removed)
        {
            var existing = presentation.Presentation.CustomShowList?.ChildElements.Where(c => c is P.CustomShow).ToList() ?? new List<OpenXmlElement>();
            var kept = existing.Where(e => !removed.Contains(Attribute(e, "name")) && !shows.Any(s => s.Name == Attribute(e, "name"))).Select(e => e.OuterXml).ToList();
            var used = existing.Select(e => uint.TryParse(Attribute(e, "id"), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0u).ToList();
            var next = used.DefaultIfEmpty(0u).Max() + 1;
            foreach (var show in shows)
            {
                var entries = string.Concat(show.RelationshipIds.Select(r => $"<p:sld r:id=\"{r}\"/>"));
                var same = existing.FirstOrDefault(e => Attribute(e, "name") == show.Name);
                if (same == null || !uint.TryParse(Attribute(same, "id"), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                {
                    id = next;
                    next++;
                }

                kept.Add($"<p:custShow name=\"{Escape(show.Name)}\" id=\"{id.ToString(CultureInfo.InvariantCulture)}\"><p:sldLst>{entries}</p:sldLst></p:custShow>");
            }

            if (kept.Count == 0)
            {
                PptPresentationOrder.Remove(presentation.Presentation, "custShowLst");
                return;
            }

            var xml = $"<p:custShowLst {Namespaces}>" + string.Concat(kept) + "</p:custShowLst>";
            PptPresentationOrder.Replace(presentation.Presentation, new P.CustomShowList(xml));
        }

        public static uint? CustomShowId(PresentationPart presentation, string name)
        {
            var shows = presentation.Presentation.CustomShowList?.ChildElements.Where(c => c is P.CustomShow);
            var match = shows?.FirstOrDefault(s => Attribute(s, "name") == name);
            return match != null && uint.TryParse(Attribute(match, "id"), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
        }

        public static uint? RangeCustomShowId(PresentationPart presentation)
        {
            var range = presentation.PresentationPropertiesPart?.PresentationProperties?.ShowProperties?.ChildElements.FirstOrDefault(c => c.LocalName == "custShow");
            return range != null && uint.TryParse(Attribute(range, "id"), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
        }

        private static void ApplyShowProperties(P.PresentationProperties properties, PptShowRequest request)
        {
            var current = properties.ShowProperties;
            var penColor = current?.ChildElements.FirstOrDefault(c => c.LocalName == "penClr")?.OuterXml;
            var extensions = new List<string>();
            var mediaValue = request.MediaControls.HasValue ? (request.MediaControls.Value ? "1" : "0") : null;
            foreach (var ext in current?.Descendants().Where(d => d.LocalName == "ext").ToList() ?? new List<OpenXmlElement>())
            {
                if (Attribute(ext, "uri") == MediaControlsUri)
                {
                    mediaValue ??= ext.Descendants().FirstOrDefault(d => d.LocalName == "showMediaCtrls") is { } media ? Attribute(media, "val") : null;
                    continue;
                }

                extensions.Add(ext.OuterXml);
            }

            if (mediaValue != null)
                extensions.Insert(0, $"<p:ext uri=\"{MediaControlsUri}\"><p14:showMediaCtrls val=\"{mediaValue}\"/></p:ext>");

            var typeXml = request.ShowType == null && current?.ChildElements.FirstOrDefault(c => c.LocalName is "present" or "browse" or "kiosk") is { } existingType
                ? existingType.OuterXml
                : (request.ShowType ?? "presenter") switch
                {
                    "browse" => "<p:browse/>",
                    "kiosk" => "<p:kiosk/>",
                    _ => "<p:present/>",
                };
            var range = request.RangeType switch
            {
                "slides" => $"<p:sldRg st=\"{request.From!.Value.ToString(CultureInfo.InvariantCulture)}\" end=\"{request.To!.Value.ToString(CultureInfo.InvariantCulture)}\"/>",
                "custom_show" => $"<p:custShow id=\"{request.CustomShowId!.Value.ToString(CultureInfo.InvariantCulture)}\"/>",
                "all" => "<p:sldAll/>",
                _ => current?.ChildElements.FirstOrDefault(c => c.LocalName is "sldAll" or "sldRg" or "custShow")?.OuterXml ?? "<p:sldAll/>",
            };
            var loop = request.Loop ?? Flag(current, "loop", false);
            var narration = request.ShowNarration ?? Flag(current, "showNarration", true);
            var animation = request.ShowAnimation ?? Flag(current, "showAnimation", true);
            var timings = request.UseTimings ?? Flag(current, "useTimings", true);
            var extXml = extensions.Count == 0 ? string.Empty : "<p:extLst>" + string.Concat(extensions) + "</p:extLst>";
            var xml = $"<p:showPr {Namespaces} loop=\"{Bit(loop)}\" showNarration=\"{Bit(narration)}\" showAnimation=\"{Bit(animation)}\" useTimings=\"{Bit(timings)}\">"
                + typeXml + range + (penColor ?? string.Empty) + extXml + "</p:showPr>";
            current?.Remove();
            var showPr = new P.ShowProperties(xml);
            var anchor = properties.ChildElements.FirstOrDefault(c => c.LocalName == "extLst" || c.LocalName == "clrMru");
            if (anchor == null)
                properties.Append(showPr);
            else
                anchor.InsertBeforeSelf(showPr);
        }

        private static bool Flag(OpenXmlElement show, string name, bool fallback)
        {
            var value = show == null ? null : Attribute(show, name);
            return value == null ? fallback : value is "1" or "true";
        }

        private static string Bit(bool value) => value ? "1" : "0";

        private static string Attribute(OpenXmlElement element, string name) =>
            element?.GetAttributes().FirstOrDefault(a => a.LocalName == name).Value;

        private static string Escape(string value) => System.Security.SecurityElement.Escape(value ?? string.Empty);
    }
}
