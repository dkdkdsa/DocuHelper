using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed record WordCharRange(string Id, int Start, int End);

    public sealed record WordCharPressed(bool Bold, bool Italic, bool Underline, bool Strike);

    public sealed record WordCharSegment(string Text, bool Bold, bool Italic, bool Underline, bool Strike);

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
            var text = map.Text;
            var start = 0;
            WordCharPressed current = null;
            for (var i = 0; i < text.Length; i++)
            {
                var flags = FlagsAt(map.PropertiesAt(i));
                if (current != null && Same(current, flags))
                    continue;

                if (current != null)
                    segments.Add(new WordCharSegment(text.Substring(start, i - start), current.Bold, current.Italic, current.Underline, current.Strike));

                current = flags;
                start = i;
            }

            if (current != null)
                segments.Add(new WordCharSegment(text.Substring(start), current.Bold, current.Italic, current.Underline, current.Strike));

            return segments;
        }

        private static WordCharPressed FlagsAt(RunProperties properties) => new(
            WordStyles.IsOn(properties?.GetFirstChild<Bold>()),
            WordStyles.IsOn(properties?.GetFirstChild<Italic>()),
            WordStyles.IsUnderlined(properties?.GetFirstChild<Underline>()),
            WordStyles.IsOn(properties?.GetFirstChild<Strike>()));

        private static bool Same(WordCharPressed left, WordCharPressed right) =>
            left.Bold == right.Bold && left.Italic == right.Italic && left.Underline == right.Underline && left.Strike == right.Strike;
    }
}
