using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordFieldUpdater
    {
        public static int Update(WordDocu docu, string scope)
        {
            var all = scope == "all";
            var count = 0;
            if (all || scope == "sequences")
                count += Sequences(docu);

            if (all || scope == "toc")
                count += WordTocBuilder.Update(docu, WordScan.Read(docu.Document));

            if (all || scope == "page_refs")
                count += References(docu, WordScan.Read(docu.Document));

            if (all)
                count += Dates(docu, DateTime.Now);

            return count;
        }

        public static int Sequences(WordDocu docu)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var changed = 0;
            foreach (var field in WordFields.Read(docu.Document.MainDocumentPart.Document.Body))
            {
                if (field.Name != "SEQ" || field.Simple != null || !field.IsComplete || field.Separate == null || Deleted(field))
                    continue;

                var label = WordCaptions.LabelOf(field);
                if (label == null)
                    continue;

                counts[label] = counts.TryGetValue(label, out var count) ? count + 1 : 1;
                if (SetResult(field, counts[label].ToString(CultureInfo.InvariantCulture)))
                    changed++;
            }

            return changed;
        }

        public static int References(WordDocu docu, WordScan scan)
        {
            var body = docu.Document.MainDocumentPart.Document.Body;
            var notes = WordInventory.Notes(docu, scan).ToDictionary(n => n.Ref, n => n.Number);
            var changed = 0;
            foreach (var field in WordFields.Read(body))
            {
                if ((field.Name != "REF" && field.Name != "PAGEREF" && field.Name != "NOTEREF") || field.Simple != null
                    || !field.IsComplete || field.Separate == null || Deleted(field))
                    continue;

                var tokens = WordFields.Tokenize(field.Instruction);
                if (tokens.Count < 2)
                    continue;

                var start = WordBookmarks.Find(docu.Document, tokens[1]);
                if (start == null)
                    continue;

                var host = field.Begin.Ancestors<Paragraph>().FirstOrDefault();
                var target = start.Ancestors<Paragraph>().FirstOrDefault();
                string value;
                switch (field.Name)
                {
                    case "PAGEREF":
                        value = target == null ? null : WordInline.PageOf(scan, target).ToString(CultureInfo.InvariantCulture);
                        break;
                    case "NOTEREF":
                        value = NoteValue(start, notes);
                        break;
                    default:
                        if (tokens.Any(t => t == "\\r" || t == "\\n" || t == "\\w"))
                            continue;

                        value = tokens.Contains("\\p") ? Position(scan, host, target) : WordBookmarks.TextOf(docu.Document, start);
                        break;
                }

                if (value != null && SetResult(field, value))
                    changed++;
            }

            return changed;
        }

        public static int Dates(WordDocu docu, DateTime now)
        {
            var changed = 0;
            foreach (var field in WordFields.Read(docu.Document.MainDocumentPart.Document.Body))
            {
                if ((field.Name != "DATE" && field.Name != "TIME") || field.Simple != null || field.Separate == null || Deleted(field))
                    continue;

                var picture = PictureOf(field.Instruction) ?? (field.Name == "DATE" ? "yyyy. M. d." : "tt h:mm");
                if (SetResult(field, Format(now, picture)))
                    changed++;
            }

            return changed;
        }

        public static string Format(DateTime value, string picture)
        {
            var format = (picture ?? string.Empty).Replace("AM/PM", "tt", StringComparison.OrdinalIgnoreCase).Replace("/", "\\/");
            try
            {
                return value.ToString(format, WordIndexEntries.Korean);
            }
            catch (FormatException)
            {
                return value.ToString("yyyy-MM-dd", WordIndexEntries.Korean);
            }
        }

        public static bool SetResult(WordField field, string value)
        {
            var first = field.Separate?.Parent as Run;
            var last = field.End?.Parent as Run;
            if (first == null || last == null || !ReferenceEquals(first.Parent, last.Parent))
                return false;

            var between = new List<OpenXmlElement>();
            var reached = false;
            for (var element = first.NextSibling(); element != null; element = element.NextSibling())
            {
                if (ReferenceEquals(element, last))
                {
                    reached = true;
                    break;
                }

                between.Add(element);
            }

            if (!reached || between.Any(e => e.Descendants<FieldChar>().Any()))
                return false;

            var text = WordText.Clean(value ?? string.Empty);
            var current = string.Concat(between.OfType<Run>().SelectMany(r => r.Elements<Text>()).Select(t => t.Text));
            if (current == text)
                return false;

            var props = first.RunProperties == null ? null : (RunProperties)first.RunProperties.CloneNode(true);
            props?.RemoveAllChildren<RunPropertiesChange>();
            foreach (var run in between.OfType<Run>().ToList())
                run.Remove();

            if (text.Length > 0)
                last.InsertBeforeSelf(WordText.NewRun(text, props != null && props.HasChildren ? props : null));

            return true;
        }

        private static bool Deleted(WordField field) => field.Begin != null && field.Begin.Ancestors<DeletedRun>().Any();

        private static string PictureOf(string instruction)
        {
            var tokens = WordFields.Tokenize(instruction);
            for (var i = 0; i + 1 < tokens.Count; i++)
            {
                if (tokens[i] == "\\@")
                    return tokens[i + 1];
            }

            return null;
        }

        private static string Position(WordScan scan, Paragraph host, Paragraph target)
        {
            var hostIndex = WordInline.BlockOf(scan, host)?.Index ?? 0;
            var targetIndex = WordInline.BlockOf(scan, target)?.Index ?? 0;
            return targetIndex < hostIndex ? "위" : "아래";
        }

        private static string NoteValue(BookmarkStart start, IReadOnlyDictionary<string, int> notes)
        {
            var element = start.NextSibling();
            if (element == null)
                return null;

            var footnote = element.Descendants<FootnoteReference>().FirstOrDefault();
            if (footnote?.Id?.Value is long footnoteId && notes.TryGetValue("fn:" + footnoteId.ToString(CultureInfo.InvariantCulture), out var number))
                return number.ToString(CultureInfo.InvariantCulture);

            var endnote = element.Descendants<EndnoteReference>().FirstOrDefault();
            if (endnote?.Id?.Value is long endnoteId && notes.TryGetValue("en:" + endnoteId.ToString(CultureInfo.InvariantCulture), out var endnoteNumber))
                return endnoteNumber.ToString(CultureInfo.InvariantCulture);

            return null;
        }
    }
}
