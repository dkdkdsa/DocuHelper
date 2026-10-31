using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordStats
    {
        public static WordCounts Count(WordDocu docu, bool includeNotes = true, WordScan scan = null)
        {
            scan ??= WordScan.Read(docu.Document);
            var charsPerLine = CharsPerLine(scan);
            var lines = 0;
            foreach (var block in scan.Blocks.Where(b => b.IsTable))
            {
                var grid = WordTables.Grid(block.Table);
                var cellChars = Math.Max(4, charsPerLine / Math.Max(1, WordTables.ColumnCount(grid)) - 2);
                foreach (var row in grid)
                {
                    var rowLines = 1;
                    foreach (var owner in row.Where(c => c.IsOrigin && c.Owner != null).Select(c => c.Owner).Distinct())
                        rowLines = Math.Max(rowLines, owner.Elements<Paragraph>().Sum(p => LinesOf(WordText.Of(p), cellChars)));

                    lines += rowLines;
                }
            }

            var paragraphs = 0;
            var words = 0;
            var characters = 0;
            var noSpaces = 0;
            foreach (var paragraph in Paragraphs(scan.Document, includeNotes))
            {
                var text = WordText.Of(paragraph);
                paragraphs++;
                words += Words(text);
                Characters(text, out var all, out var visible);
                characters += all;
                noSpaces += visible;
                if (!paragraph.Ancestors<Table>().Any())
                    lines += LinesOf(text, charsPerLine);
            }

            var tables = scan.Blocks.Count(b => b.IsTable);
            var objects = WordInventory.Objects(docu, scan).Count;
            return new WordCounts(scan.PageCount, words, characters, noSpaces, paragraphs, lines, tables, objects);
        }

        public static WordCounts Count(string text)
        {
            var value = text ?? string.Empty;
            Characters(value, out var all, out var visible);
            var paragraphs = value.Length == 0 ? 0 : value.Split('\n').Length;
            return new WordCounts(0, Words(value), all, visible, paragraphs, LinesOf(value, CharsPerLine(null)), 0, 0);
        }

        public static int Words(string text)
        {
            var count = 0;
            var inside = false;
            foreach (var c in text ?? string.Empty)
            {
                if (char.IsWhiteSpace(c) && c != ' ')
                {
                    inside = false;
                }
                else if (!inside)
                {
                    inside = true;
                    count++;
                }
            }

            return count;
        }

        public static IEnumerable<Paragraph> Paragraphs(WordprocessingDocument document, bool includeNotes)
        {
            var main = document?.MainDocumentPart;
            var body = main?.Document?.Body;
            if (body != null)
            {
                foreach (var paragraph in body.Descendants<Paragraph>())
                {
                    if (!paragraph.Ancestors<AlternateContentFallback>().Any())
                        yield return paragraph;
                }
            }

            if (!includeNotes || main == null)
                yield break;

            foreach (var note in (main.FootnotesPart?.Footnotes?.Elements<Footnote>() ?? Enumerable.Empty<Footnote>()).Where(IsReal))
            {
                foreach (var paragraph in note.Descendants<Paragraph>())
                    yield return paragraph;
            }

            foreach (var note in (main.EndnotesPart?.Endnotes?.Elements<Endnote>() ?? Enumerable.Empty<Endnote>()).Where(IsReal))
            {
                foreach (var paragraph in note.Descendants<Paragraph>())
                    yield return paragraph;
            }
        }

        public static bool IsReal(FootnoteEndnoteType note) =>
            note.Type == null || note.Type.Value == FootnoteEndnoteValues.Normal;

        private static void Characters(string text, out int all, out int visible)
        {
            all = 0;
            visible = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\n' || c == '\r')
                    continue;

                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    i++;

                all++;
                if (!char.IsWhiteSpace(c))
                    visible++;
            }
        }

        private static double CharsPerLine(WordScan scan)
        {
            WordPager.TextArea(scan?.Body, out var width, out _);
            var font = 20.0 / Math.Max(10, scan?.Styles.NormalFontSize ?? 20);
            return Math.Max(10, 45.0 * width / WordPager.DefaultTextWidth * font);
        }

        private static int LinesOf(string text, double charsPerLine)
        {
            var lines = 0;
            foreach (var line in text.Split('\n'))
                lines += (int)Math.Max(1, Math.Ceiling(WordPager.Width(line) / charsPerLine));

            return lines;
        }
    }
}
