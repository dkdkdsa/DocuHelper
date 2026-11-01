using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    internal static class HwpWordFields
    {
        internal const string LinkPrefix = "hwp-link:";

        private static int s_bookmarkId = 100000;

        internal static void Append(Paragraph paragraph, IEnumerable<HwpRun> runs)
        {
            var holder = new Paragraph();
            var scopes = new Dictionary<OpenXmlElement, HwpRun>(ReferenceEqualityComparer.Instance);
            foreach (var run in runs)
            {
                var before = holder.ChildElements.Count();
                Add(holder, run);
                foreach (var child in holder.ChildElements.Skip(before).ToList())
                    scopes[child] = run;
            }

            Hyperlink link = null;
            string linkKey = null;
            SdtRun control = null;
            HwpClickHere controlKey = null;
            SdtContentRun content = null;
            foreach (var child in holder.ChildElements.ToList())
            {
                child.Remove();
                var run = scopes.TryGetValue(child, out var found) ? found : null;
                if (run?.ClickHere != null)
                {
                    if (control == null || !ReferenceEquals(controlKey, run.ClickHere))
                    {
                        control = Control(run.ClickHere);
                        content = control.SdtContentRun;
                        paragraph.AppendChild(control);
                        controlKey = run.ClickHere;
                    }

                    link = null;
                    linkKey = null;
                    content.AppendChild(child);
                    continue;
                }

                control = null;
                controlKey = null;
                content = null;
                if (run?.Link != null)
                {
                    if (link == null || linkKey != run.Link)
                    {
                        link = Hyper(run.Link);
                        paragraph.AppendChild(link);
                        linkKey = run.Link;
                    }

                    link.AppendChild(child);
                    continue;
                }

                link = null;
                linkKey = null;
                paragraph.AppendChild(child);
            }
        }

        internal static void ResolveLinks(WordprocessingDocument word)
        {
            var main = word.MainDocumentPart;
            ResolveIn(main, main.Document);
            foreach (var header in main.HeaderParts)
                ResolveIn(header, header.Header);
            foreach (var footer in main.FooterParts)
                ResolveIn(footer, footer.Footer);
        }

        private static void ResolveIn(OpenXmlPart part, OpenXmlElement root)
        {
            foreach (var link in root.Descendants<Hyperlink>().Where(h => h.Id?.Value != null && h.Id.Value.StartsWith(LinkPrefix, StringComparison.Ordinal)).ToList())
            {
                var target = link.Id.Value.Substring(LinkPrefix.Length);
                if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
                {
                    link.Id = null;
                    continue;
                }

                link.Id = part.AddHyperlinkRelationship(uri, true).Id;
            }
        }

        private static void Add(Paragraph holder, HwpRun run)
        {
            if (run.Bookmark != null)
            {
                var id = Interlocked.Increment(ref s_bookmarkId).ToString(System.Globalization.CultureInfo.InvariantCulture);
                holder.AppendChild(new BookmarkStart { Id = id, Name = run.Bookmark });
                holder.AppendChild(new BookmarkEnd { Id = id });
                return;
            }

            if (run.Picture != null)
            {
                holder.AppendChild(new Run(HwpWordPictures.PictureDrawing(run.Picture)));
                return;
            }

            if (run.Field == "PAGE")
                HwpWordRuns.AppendField(holder, run);
            else
                HwpWordRuns.AppendRun(holder, run.Text, run);
        }

        private static Hyperlink Hyper(string target) =>
            target.StartsWith("#", StringComparison.Ordinal)
                ? new Hyperlink { Anchor = target.Substring(1) }
                : new Hyperlink { Id = LinkPrefix + target };

        private static SdtRun Control(HwpClickHere control)
        {
            var properties = new SdtProperties();
            if (!string.IsNullOrEmpty(control.Guide))
                properties.AppendChild(new SdtAlias { Val = control.Guide });

            if (!string.IsNullOrEmpty(control.Name))
                properties.AppendChild(new Tag { Val = control.Name });

            if (control.Placeholder)
                properties.AppendChild(new ShowingPlaceholder());

            return new SdtRun(properties, new SdtContentRun());
        }
    }
}
