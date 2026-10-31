using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordRevisions
    {
        public static string DefaultAuthor { get; set; }

        public static bool Tracking(WordDocu docu)
        {
            var flag = docu?.Document?.MainDocumentPart?.DocumentSettingsPart?.Settings?.GetFirstChild<TrackRevisions>();
            return flag != null && WordStyles.IsOn(flag);
        }

        public static void SetTracking(WordDocu docu, bool on)
        {
            var settings = WordParts.EnsureSettings(docu);
            settings.RemoveAllChildren<TrackRevisions>();
            if (on)
                settings.AddChild(new TrackRevisions());
        }

        public static RevisionContext Active(WordDocu docu)
        {
            if (docu == null || docu is HwpDocu)
                return null;

            var assistant = string.IsNullOrWhiteSpace(docu.AssistantRevisionAuthor) ? null : docu.AssistantRevisionAuthor.Trim();
            if (assistant == null && !Tracking(docu) && WordProtection.Mode(docu) != WordProtectionMode.TrackedChangesOnly)
                return null;

            var author = assistant ?? (string.IsNullOrWhiteSpace(DefaultAuthor) ? Environment.UserName : DefaultAuthor.Trim());
            var now = DateTime.UtcNow;
            var date = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc);
            return new RevisionContext(author, date, WordParts.NextRevisionId(docu), assistant != null);
        }

        public static InsertedRun WrapInserted(IEnumerable<Run> runs, RevisionContext ctx)
        {
            InsertedRun first = null;
            foreach (var group in Groups(runs))
            {
                if (group[0].Parent is InsertedRun existing)
                {
                    first ??= existing;
                    continue;
                }

                var wrapper = ctx.StampRun(new InsertedRun());
                group[0].InsertBeforeSelf(wrapper);
                foreach (var run in group)
                {
                    run.Remove();
                    wrapper.AppendChild(run);
                }

                first ??= wrapper;
            }

            return first;
        }

        public static DeletedRun MarkDeleted(IEnumerable<Run> runs, RevisionContext ctx)
        {
            var pending = new List<Run>();
            foreach (var run in runs.Where(r => r.Parent != null).ToList())
            {
                if (run.Parent is DeletedRun)
                    continue;

                if (run.Parent is InsertedRun inserted && string.Equals(inserted.Author?.Value, ctx.Author, StringComparison.Ordinal))
                {
                    run.Remove();
                    if (!inserted.ChildElements.Any(c => c is Run))
                        inserted.Remove();

                    continue;
                }

                pending.Add(run);
            }

            DeletedRun first = null;
            foreach (var group in Groups(pending))
            {
                foreach (var run in group)
                    Hide(run);

                var wrapper = ctx.StampRun(new DeletedRun());
                group[0].InsertBeforeSelf(wrapper);
                foreach (var run in group)
                {
                    run.Remove();
                    wrapper.AppendChild(run);
                }

                first ??= wrapper;
            }

            return first;
        }

        public static void MarkParagraphMark(Paragraph paragraph, bool inserted, RevisionContext ctx)
        {
            var properties = paragraph.ParagraphProperties ?? (paragraph.ParagraphProperties = new ParagraphProperties());
            var mark = properties.ParagraphMarkRunProperties ?? (properties.ParagraphMarkRunProperties = new ParagraphMarkRunProperties());
            if (inserted)
            {
                mark.RemoveAllChildren<Inserted>();
                mark.InsertAt(ctx.Stamp(new Inserted()), 0);
            }
            else
            {
                mark.RemoveAllChildren<Deleted>();
                mark.InsertAt(ctx.Stamp(new Deleted()), mark.Elements<Inserted>().Any() ? 1 : 0);
            }
        }

        public static RunPropertiesChange RecordFormat(RunProperties old, RevisionContext ctx)
        {
            var previous = new PreviousRunProperties();
            if (old != null)
            {
                foreach (var child in old.ChildElements.Where(c => c is not RunPropertiesChange))
                    previous.AppendChild(child.CloneNode(true));
            }

            return ctx.Stamp(new RunPropertiesChange(previous));
        }

        private static List<List<Run>> Groups(IEnumerable<Run> runs)
        {
            var groups = new List<List<Run>>();
            Run previous = null;
            foreach (var run in runs.Where(r => r.Parent != null))
            {
                if (previous != null && ReferenceEquals(previous.Parent, run.Parent) && ReferenceEquals(previous.NextSibling(), run))
                {
                    groups[^1].Add(run);
                }
                else
                {
                    groups.Add(new List<Run> { run });
                }

                previous = run;
            }

            return groups;
        }

        private static void Hide(Run run)
        {
            foreach (var text in run.Elements<Text>().ToList())
            {
                var deleted = new DeletedText { Text = text.Text, Space = SpaceProcessingModeValues.Preserve };
                text.InsertBeforeSelf(deleted);
                text.Remove();
            }

            foreach (var code in run.Elements<FieldCode>().ToList())
            {
                var deleted = new DeletedFieldCode { Text = code.Text, Space = SpaceProcessingModeValues.Preserve };
                code.InsertBeforeSelf(deleted);
                code.Remove();
            }
        }
    }
}
