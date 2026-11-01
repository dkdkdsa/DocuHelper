using System;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordFootnotes
    {
        private const double TextPt = 10;
        private const double LinePt = 12;
        private const double GapPt = 2;
        private const double RulePt = 12;

        public static double Height(WordLayoutContext context, Paragraph paragraph, double widthPt)
        {
            if (paragraph == null || widthPt <= 0)
                return 0;

            var part = context.Document?.MainDocumentPart?.FootnotesPart;
            var total = 0.0;
            foreach (var reference in paragraph.Descendants<FootnoteReference>())
            {
                var note = part?.Footnotes?.Elements<Footnote>().FirstOrDefault(f => f.Id?.Value == reference.Id?.Value && WordStats.IsReal(f));
                var text = note == null ? string.Empty : string.Join("\n", note.Descendants<Paragraph>().Select(WordText.Of));
                var lines = Math.Max(1, Math.Ceiling(WordPager.Width(text) * TextPt / widthPt));
                total += lines * LinePt + GapPt;
            }

            return total > 0 ? total + RulePt : 0;
        }
    }
}
