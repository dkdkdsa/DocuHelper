using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordBookmarks
    {
        public const int MaxLength = 40;
        public const string CrossReferencePrefix = "_Ref";
        public const string TocPrefix = "_Toc";

        private static readonly Regex s_name = new(@"^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
        private static readonly Regex s_separators = new(@"[\s""\\]+", RegexOptions.CultureInvariant);

        public static string CheckName(string name)
        {
            var value = (name ?? string.Empty).Trim();
            if (value.Length == 0 || value.Length > MaxLength || !s_name.IsMatch(value))
                throw new ToolInputException("책갈피 이름은 영문자로 시작하고 영문자, 숫자, 밑줄(_)만 쓸 수 있으며 40자 이내여야 해요.");

            return value;
        }

        public static IEnumerable<BookmarkStart> Starts(WordprocessingDocument document) =>
            document.MainDocumentPart.Document.Body.Descendants<BookmarkStart>();

        public static BookmarkStart Find(WordprocessingDocument document, string name) =>
            Starts(document).FirstOrDefault(b => b.Name?.Value == name);

        public static BookmarkEnd EndOf(WordprocessingDocument document, BookmarkStart start) =>
            document.MainDocumentPart.Document.Body.Descendants<BookmarkEnd>().FirstOrDefault(e => e.Id?.Value == start.Id?.Value);

        public static string NewHidden(WordprocessingDocument document, string prefix)
        {
            var used = new HashSet<string>(Starts(document).Select(b => b.Name?.Value), StringComparer.Ordinal);
            while (true)
            {
                var name = prefix + Random.Shared.Next(100000000, 1000000000).ToString(CultureInfo.InvariantCulture);
                if (used.Add(name))
                    return name;
            }
        }

        public static BookmarkStart Insert(WordDocu docu, WordTextMap map, int start, int end, string name)
        {
            var id = WordParts.NextBookmarkId(docu).ToString(CultureInfo.InvariantCulture);
            var open = new BookmarkStart { Id = id, Name = name };
            var close = new BookmarkEnd { Id = id };
            var runs = start < end ? map.Isolate(start, end) : null;
            if (runs == null || runs.Count == 0)
            {
                WordInline.Place(map, start, new OpenXmlElement[] { open, close });
            }
            else
            {
                runs[0].InsertBeforeSelf(open);
                runs[^1].InsertAfterSelf(close);
            }

            return open;
        }

        public static void Around(WordDocu docu, OpenXmlElement first, OpenXmlElement last, string name)
        {
            var id = WordParts.NextBookmarkId(docu).ToString(CultureInfo.InvariantCulture);
            first.InsertBeforeSelf(new BookmarkStart { Id = id, Name = name });
            last.InsertAfterSelf(new BookmarkEnd { Id = id });
        }

        public static string Existing(WordprocessingDocument document, OpenXmlElement first, OpenXmlElement last, string prefix)
        {
            foreach (var start in Starts(document).ToList())
            {
                var name = start.Name?.Value;
                if (name == null || !name.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                var end = EndOf(document, start);
                if (end != null && ReferenceEquals(start.NextSibling(), first) && ReferenceEquals(end.PreviousSibling(), last))
                    return name;
            }

            return null;
        }

        public static string InParagraph(Paragraph paragraph, string prefix) =>
            paragraph.Descendants<BookmarkStart>().Select(b => b.Name?.Value)
                .FirstOrDefault(n => n != null && n.StartsWith(prefix, StringComparison.Ordinal));

        public static void Remove(WordprocessingDocument document, BookmarkStart start)
        {
            var end = EndOf(document, start);
            start.Remove();
            end?.Remove();
        }

        public static string TextOf(WordprocessingDocument document, BookmarkStart start)
        {
            var id = start.Id?.Value;
            var builder = new StringBuilder();
            var inside = false;
            foreach (var element in document.MainDocumentPart.Document.Body.Descendants())
            {
                if (element is BookmarkStart open && open.Id?.Value == id)
                {
                    inside = true;
                    continue;
                }

                if (element is BookmarkEnd close && close.Id?.Value == id)
                    break;

                if (!inside)
                    continue;

                if (element is Text text)
                    builder.Append(text.Text);
                else if (element is TabChar)
                    builder.Append('\t');
            }

            return builder.ToString();
        }

        public static void DropUnused(WordprocessingDocument document, IEnumerable<string> names)
        {
            var body = document.MainDocumentPart.Document.Body;
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var link in body.Descendants<Hyperlink>())
            {
                if (link.Anchor?.Value != null)
                    referenced.Add(link.Anchor.Value);
            }

            foreach (var code in body.Descendants<FieldCode>())
            {
                foreach (var token in s_separators.Split(code.Text ?? string.Empty))
                    referenced.Add(token);
            }

            foreach (var name in names.Distinct().ToList())
            {
                if (referenced.Contains(name))
                    continue;

                var start = Find(document, name);
                if (start != null)
                    Remove(document, start);
            }
        }
    }
}
