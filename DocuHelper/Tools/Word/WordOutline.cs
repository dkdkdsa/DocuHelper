using System;
using System.Collections.Generic;
using System.Globalization;
using DocuHelper.Docus;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordOutline
    {
        private static readonly string[] s_prefixes = { "heading ", "제목 ", "개요 " };

        public static IReadOnlyList<WordHeading> Headings(WordDocu docu) => Headings(WordScan.Read(docu.Document));

        public static IReadOnlyList<WordHeading> Headings(WordScan scan)
        {
            var headings = new List<WordHeading>();
            foreach (var block in scan.Blocks)
            {
                if (block.IsTable || !block.IsHeading)
                    continue;

                headings.Add(new WordHeading(block.Index, block.Id, LevelOf(scan.Styles, block.Paragraph, block.Kind), block.Text.Trim(), block.Page, block.IsNumbered));
            }

            return headings;
        }

        public static int LevelOf(WordStyles styles, Paragraph paragraph, WordKind kind)
        {
            if (kind == WordKind.Title)
                return 0;

            foreach (var style in styles.ParagraphChain(paragraph))
            {
                var name = WordStyles.NameOf(style);
                if (name == null)
                    continue;

                foreach (var prefix in s_prefixes)
                {
                    if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.Ordinal)
                        && int.TryParse(name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var level) && level >= 1 && level <= 9)
                        return level;
                }
            }

            var direct = paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value;
            if (direct != null && direct.Value < 9)
                return direct.Value + 1;

            foreach (var style in styles.ParagraphChain(paragraph))
            {
                var level = style.StyleParagraphProperties?.OutlineLevel?.Val?.Value;
                if (level != null && level.Value < 9)
                    return level.Value + 1;
            }

            return kind == WordKind.Heading1 ? 1 : 2;
        }

        public static string Label(WordHeading heading) => heading.Level == 0 ? "title" : "heading" + heading.Level.ToString(CultureInfo.InvariantCulture);
    }
}
