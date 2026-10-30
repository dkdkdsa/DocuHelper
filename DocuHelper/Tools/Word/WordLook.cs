using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class WordLook
    {
        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public bool Centered { get; set; }

        public int FontSize { get; set; }

        public string Color { get; set; }

        public static WordLook Of(WordScan scan, WordBlock block)
        {
            var styles = scan.Styles;
            var look = new WordLook();
            var paragraphs = block.IsTable ? block.Table.Descendants<Paragraph>().ToList() : new List<Paragraph> { block.Paragraph };
            var runs = paragraphs.SelectMany(p => WordText.TextRuns(p).Select(r => (Run: r, Paragraph: p))).ToList();
            if (runs.Count == 0)
            {
                var paragraph = paragraphs.FirstOrDefault();
                look.Bold = WordStyles.IsOn(styles.MarkValue<Bold>(paragraph));
                look.Italic = WordStyles.IsOn(styles.MarkValue<Italic>(paragraph));
                look.Underline = WordStyles.IsUnderlined(styles.MarkValue<Underline>(paragraph));
                look.FontSize = SizeOf(styles.MarkValue<FontSize>(paragraph));
                look.Color = styles.MarkValue<Color>(paragraph)?.Val?.Value;
            }
            else
            {
                look.Bold = runs.All(x => WordStyles.IsOn(styles.RunValue<Bold>(x.Run, x.Paragraph)));
                look.Italic = runs.All(x => WordStyles.IsOn(styles.RunValue<Italic>(x.Run, x.Paragraph)));
                look.Underline = runs.All(x => WordStyles.IsUnderlined(styles.RunValue<Underline>(x.Run, x.Paragraph)));
                look.FontSize = SizeOf(styles.RunValue<FontSize>(runs[0].Run, runs[0].Paragraph));
                look.Color = styles.RunValue<Color>(runs[0].Run, runs[0].Paragraph)?.Val?.Value;
            }

            if (!block.IsTable)
                look.Centered = styles.IsCentered(block.Paragraph);

            return look;
        }

        private static int SizeOf(FontSize size) => int.TryParse(size?.Val?.Value, out var value) ? value : 0;
    }
}
