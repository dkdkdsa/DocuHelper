using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed record WordCharRange(string Id, int Start, int End);

    public sealed record WordCharPressed(bool Bold, bool Italic, bool Underline, bool Strike);

    public sealed record WordCharSegment(string Text, WordCharLook Look);

    public static class WordCharFormat
    {
        public static WordCharPressed Pressed(WordDocu docu, IEnumerable<WordCharRange> ranges)
        {
            if (docu?.Document == null || ranges == null)
                return new WordCharPressed(false, false, false, false);

            var scan = WordScan.Read(docu.Document);
            var count = 0;
            var bold = true;
            var italic = true;
            var underline = true;
            var strike = true;
            foreach (var range in ranges)
            {
                var paragraph = scan.Find(range.Id)?.Paragraph;
                if (paragraph == null)
                    continue;

                var map = WordTextMap.Build(paragraph);
                for (var i = Math.Max(0, range.Start); i < Math.Min(range.End, map.Length); i++)
                {
                    count++;
                    var flags = FlagsAt(map.PropertiesAt(i));
                    bold &= flags.Bold;
                    italic &= flags.Italic;
                    underline &= flags.Underline;
                    strike &= flags.Strike;
                }
            }

            return new WordCharPressed(count > 0 && bold, count > 0 && italic, count > 0 && underline, count > 0 && strike);
        }

        public static IReadOnlyList<WordCharSegment> Segments(Paragraph paragraph)
        {
            var map = WordTextMap.Build(paragraph);
            var segments = new List<WordCharSegment>();
            var buffer = new StringBuilder();
            WordCharLook current = null;
            void Flush()
            {
                if (buffer.Length > 0)
                    segments.Add(new WordCharSegment(buffer.ToString(), current));

                buffer.Clear();
            }

            foreach (var segment in map.Segments)
            {
                if (segment.AtomKind == WordAtomKind.Note || segment.AtomKind == WordAtomKind.Bookmark)
                {
                    var mark = MarkOf(segment.Element);
                    if (mark == null)
                        continue;

                    Flush();
                    current = null;
                    segments.Add(new WordCharSegment(string.Empty, WordLooks.CharOf(null, false, mark.Value.Kind, mark.Value.Text)));
                    continue;
                }

                if (segment.Length == 0)
                    continue;

                var look = WordLooks.CharOf(segment.Run?.RunProperties, map.InHyperlink(segment.Start));
                if (current != null && !current.Equals(look))
                    Flush();

                current = look;
                buffer.Append(map.Text, segment.Start, segment.Length);
            }

            Flush();
            return segments;
        }

        private static (string Kind, string Text)? MarkOf(OpenXmlElement element) => element switch
        {
            FootnoteReference note => ("fn", note.Id?.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            EndnoteReference note => ("en", note.Id?.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            BookmarkStart bookmark when !(bookmark.Name?.Value ?? "_").StartsWith("_", StringComparison.Ordinal) => ("bm", bookmark.Name?.Value),
            _ => null,
        };

        private static WordCharPressed FlagsAt(RunProperties properties) => new(
            WordStyles.IsOn(properties?.GetFirstChild<Bold>()),
            WordStyles.IsOn(properties?.GetFirstChild<Italic>()),
            WordStyles.IsUnderlined(properties?.GetFirstChild<Underline>()),
            WordStyles.IsOn(properties?.GetFirstChild<Strike>()));
    }
}
