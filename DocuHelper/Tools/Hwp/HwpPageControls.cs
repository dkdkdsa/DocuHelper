using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Hwp
{
    internal static class HwpPageControls
    {
        internal const string HidePrefix = "_hwpHide_";
        internal const string NewNumberPrefix = "_hwpNewNum_";
        private const string FlagOrder = "hfpbg";

        internal static readonly IReadOnlyDictionary<string, char> Letters = new Dictionary<string, char>(StringComparer.Ordinal)
        {
            ["header"] = 'h',
            ["footer"] = 'f',
            ["page_number"] = 'p',
            ["border"] = 'b',
            ["background"] = 'g',
        };

        internal static readonly IReadOnlyDictionary<char, string> Names = new Dictionary<char, string>
        {
            ['h'] = "머리말",
            ['f'] = "꼬리말",
            ['p'] = "쪽 번호",
            ['b'] = "쪽 테두리",
            ['g'] = "쪽 배경",
        };

        internal static string Flags(IEnumerable<char> letters) =>
            new string(FlagOrder.Where(letters.Contains).ToArray());

        internal static string Words(string flags) =>
            string.Join("·", flags.Select(c => Names[c]));

        internal static IEnumerable<(WordBlock Block, BookmarkStart Start)> Marks(WordScan scan, int page, string prefix)
        {
            foreach (var block in scan.Blocks.Where(b => b.Page == page && b.Paragraph != null))
            {
                foreach (var start in block.Paragraph.Elements<BookmarkStart>().Where(s => (s.Name?.Value ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal)).ToList())
                    yield return (block, start);
            }
        }

        internal static string FlagsOf(BookmarkStart start) => (start.Name?.Value ?? string.Empty).Substring(HidePrefix.Length);

        internal static void Remove(BookmarkStart start)
        {
            var end = start.Parent?.Elements<BookmarkEnd>().FirstOrDefault(e => e.Id?.Value == start.Id?.Value);
            end?.Remove();
            start.Remove();
        }

        internal static void Add(Paragraph paragraph, string name, int id)
        {
            var key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var start = new BookmarkStart { Id = key, Name = name };
            var end = new BookmarkEnd { Id = key };
            var anchor = paragraph.ParagraphProperties;
            if (anchor != null)
                anchor.InsertAfterSelf(start);
            else
                paragraph.PrependChild(start);

            start.InsertAfterSelf(end);
        }
    }
}
