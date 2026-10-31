using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordInline
    {
        public static WordBlock BlockOf(WordScan scan, OpenXmlElement element)
        {
            if (element == null)
                return null;

            return scan.Blocks.FirstOrDefault(b => ReferenceEquals(b.Element, element))
                ?? scan.Blocks.FirstOrDefault(b => b.IsTable && element.Ancestors<Table>().Any(t => ReferenceEquals(t, b.Table)));
        }

        public static string LocationOf(WordScan scan, OpenXmlElement element)
        {
            var block = BlockOf(scan, element);
            return block == null ? null : WordScan.PageLabel(new[] { block });
        }

        public static int PageOf(WordScan scan, OpenXmlElement element) => BlockOf(scan, element)?.Page ?? 1;

        public static IReadOnlyList<Run> Place(WordTextMap map, int offset, IEnumerable<OpenXmlElement> elements)
        {
            var items = elements.ToList();
            var tail = offset < map.Length ? map.Isolate(offset, map.Length) : Array.Empty<Run>();
            if (tail.Count > 0)
            {
                foreach (var item in items)
                    tail[0].InsertBeforeSelf(item);
            }
            else
            {
                foreach (var item in items)
                    map.Paragraph.AppendChild(item);
            }

            return items.OfType<Run>().ToList();
        }

        public static void Track(RevisionContext ctx, IReadOnlyList<Run> runs)
        {
            if (ctx != null && runs.Count > 0)
                WordRevisions.WrapInserted(runs, ctx);
        }

        public static WordTextSpan FieldAt(WordTextMap map, int offset, params string[] names) =>
            map.Spans.FirstOrDefault(s => s.Kind == WordSpanKind.ComplexField && names.Contains(WordFields.Name(s.Instruction))
                && s.Start <= offset && offset <= s.End);

        public static IReadOnlyList<Run> FieldRuns(WordField field)
        {
            var first = field.Begin?.Parent as Run;
            var last = field.End?.Parent as Run;
            if (first == null || last == null || !ReferenceEquals(first.Parent, last.Parent))
                return null;

            var runs = new List<Run>();
            for (OpenXmlElement element = first; element != null; element = element.NextSibling())
            {
                if (element is Run run)
                    runs.Add(run);

                if (ReferenceEquals(element, last))
                    return runs;
            }

            return null;
        }

        public static bool Drop(WordField field, RevisionContext ctx)
        {
            var runs = FieldRuns(field);
            if (runs == null)
                return false;

            if (ctx == null)
            {
                foreach (var run in runs)
                    run.Remove();
            }
            else
            {
                WordRevisions.MarkDeleted(runs, ctx);
            }

            return true;
        }
    }
}
