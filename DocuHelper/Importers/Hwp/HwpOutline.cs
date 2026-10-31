using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocuHelper.Docus;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Importers.Hwp
{
    public static class HwpOutline
    {
        public const int DefaultLength = 24;

        private const string Letters = "가나다라마바사아자차카타파하";
        private const string Ellipsis = "…";
        private const int BodyTextLevel = 9;

        private static readonly Regex s_spaces = new(@"\s+", RegexOptions.CultureInvariant);

        private static readonly (Regex Pattern, int Level)[] s_patterns =
        {
            (new Regex(@"^\d{1,3}\.\s+\S", RegexOptions.CultureInvariant), 1),
            (new Regex("^[" + Letters + @"]\.\s+\S", RegexOptions.CultureInvariant), 2),
            (new Regex(@"^\d{1,3}\)\s+\S", RegexOptions.CultureInvariant), 3),
            (new Regex("^[" + Letters + @"]\)\s+\S", RegexOptions.CultureInvariant), 4),
            (new Regex(@"^\(\d{1,3}\)\s*\S", RegexOptions.CultureInvariant), 5),
            (new Regex(@"^\([" + Letters + @"]\)\s*\S", RegexOptions.CultureInvariant), 6),
            (new Regex(@"^[①-⑳]\s*\S", RegexOptions.CultureInvariant), 7),
            (new Regex(@"^[㉮-㉻]\s*\S", RegexOptions.CultureInvariant), 8),
            (new Regex(@"^붙임(?![가-힣])", RegexOptions.CultureInvariant), 1),
        };

        public static IReadOnlyList<HwpOutlineItem> Detect(WordDocu docu, int length = DefaultLength) =>
            docu?.Document == null ? new List<HwpOutlineItem>() : Detect(WordScan.Read(docu.Document), length);

        public static IReadOnlyList<HwpOutlineItem> Detect(WordScan scan, int length = DefaultLength)
        {
            var items = new List<HwpOutlineItem>();
            if (scan == null)
                return items;

            foreach (var block in scan.Blocks)
            {
                if (block.IsTable || block.IsEmpty || block.Id == null)
                    continue;

                var level = block.IsHeading ? HeadingLevel(scan, block) : PatternLevel(block.Text);
                if (level != null)
                    items.Add(new HwpOutlineItem(block.Id, level.Value, Shorten(block.Text, length)));
            }

            return items;
        }

        private static int? HeadingLevel(WordScan scan, WordBlock block)
        {
            var chain = scan.Styles.ParagraphChain(block.Paragraph).ToList();
            foreach (var style in chain)
            {
                if (HwpStyleNames.OutlineLevel(WordStyles.NameOf(style)) is int named)
                    return named;
            }

            if (OutlineLevelOf(block.Paragraph.ParagraphProperties?.OutlineLevel) is int direct)
                return direct;

            foreach (var style in chain)
            {
                if (OutlineLevelOf(style.StyleParagraphProperties?.OutlineLevel) is int inherited)
                    return inherited;
            }

            return block.Kind == WordKind.Heading2 ? 2 : 1;
        }

        private static int? OutlineLevelOf(OutlineLevel outline)
        {
            var value = outline?.Val?.Value;
            return value != null && value.Value >= 0 && value.Value < BodyTextLevel ? value.Value + 1 : null;
        }

        private static int? PatternLevel(string text)
        {
            var value = (text ?? string.Empty).Trim();
            foreach (var (pattern, level) in s_patterns)
            {
                if (pattern.IsMatch(value))
                    return level;
            }

            return null;
        }

        private static string Shorten(string text, int length)
        {
            var line = s_spaces.Replace(text ?? string.Empty, " ").Trim();
            if (length <= 0 || line.Length <= length)
                return line;

            var cut = char.IsHighSurrogate(line[length - 1]) ? length - 1 : length;
            return line.Substring(0, cut).TrimEnd() + Ellipsis;
        }
    }
}
