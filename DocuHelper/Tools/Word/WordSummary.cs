using System;
using System.Linq;
using System.Text;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Word
{
    public static class WordSummary
    {
        private const int MaxLength = 300;
        private const int TitleLength = 40;
        private const int HeadingLength = 30;

        public static string Describe(WordDocu docu)
        {
            var scan = WordScan.Read(docu.Document);
            var blocks = scan.Blocks;
            if (blocks.All(b => b.IsEmpty))
                return "빈 문서";

            var tables = blocks.Count(b => b.IsTable);
            var paragraphs = blocks.Count - tables;
            var text = new StringBuilder();
            text.Append(tables > 0 ? $"문단 {paragraphs}개, 표 {tables}개, 약 {scan.PageCount}쪽" : $"문단 {paragraphs}개, 약 {scan.PageCount}쪽");

            var first = blocks.FirstOrDefault(b => !b.IsEmpty);
            var title = blocks.FirstOrDefault(b => b.Kind == WordKind.Title && !b.IsEmpty)
                ?? (first != null && first.Kind == WordKind.Heading1 ? first : null);
            if (title != null)
                text.Append(" · 제목: ").Append(Shorten(title.Text, TitleLength));

            var headings = blocks
                .Where(b => b != title && !b.IsEmpty && (b.Kind == WordKind.Heading1 || b.Kind == WordKind.Heading2))
                .Select(b => Shorten(b.Text, HeadingLength))
                .ToList();

            if (headings.Count > 0)
            {
                text.Append(" · 소제목: ");
                for (var i = 0; i < headings.Count; i++)
                {
                    var piece = (i > 0 ? ", " : string.Empty) + headings[i];
                    if (text.Length + piece.Length > MaxLength - 3)
                    {
                        text.Append(", …");
                        break;
                    }

                    text.Append(piece);
                }
            }
            else if (title == null && first != null)
            {
                text.Append(" · 첫 문단: ").Append(Shorten(first.IsTable ? "표" : first.Text, 60));
            }

            return text.ToString();
        }

        private static string Shorten(string text, int limit)
        {
            var value = string.Join(" ", (text ?? string.Empty).Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            return value.Length <= limit ? value : value.Substring(0, limit) + "…";
        }
    }
}
