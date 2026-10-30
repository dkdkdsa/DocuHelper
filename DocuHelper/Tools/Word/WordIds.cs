using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordIds
    {
        public const string ParagraphPrefix = "p:";
        public const string TablePrefix = "t:";

        private const string W14Namespace = "http://schemas.microsoft.com/office/word/2010/wordml";
        private const string McNamespace = "http://schemas.openxmlformats.org/markup-compatibility/2006";
        private const uint Limit = 0x80000000;

        public static string Of(Paragraph paragraph)
        {
            var value = paragraph?.ParagraphId?.Value;
            return IsValid(value) ? value.ToUpperInvariant() : null;
        }

        public static string ParagraphAnchor(string id) => ParagraphPrefix + id;

        public static string TableAnchor(string id) => TablePrefix + id;

        public static string Normalize(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var value = id.Trim().Trim('[', ']', '#', '"', '\'').Trim();
            if (value.StartsWith(ParagraphPrefix, StringComparison.OrdinalIgnoreCase) || value.StartsWith(TablePrefix, StringComparison.OrdinalIgnoreCase))
                value = value.Substring(2).Trim();

            if (value.StartsWith("id=", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(3).Trim();

            return value.ToUpperInvariant();
        }

        public static bool IsValid(string value)
        {
            if (value == null || value.Length != 8)
                return false;

            if (!uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var number))
                return false;

            return number > 0 && number < Limit;
        }

        public static void Prepare(WordprocessingDocument document)
        {
            var root = document?.MainDocumentPart?.Document;
            if (root?.Body == null)
                return;

            DeclareNamespaces(root);

            var used = new HashSet<string>(OtherPartIds(document), StringComparer.Ordinal);
            var paragraphs = root.Body.Descendants<Paragraph>().ToList();
            var pending = new List<Paragraph>();
            foreach (var paragraph in paragraphs)
            {
                var id = Of(paragraph);
                if (id == null || !used.Add(id))
                    pending.Add(paragraph);
            }

            var random = new Random(paragraphs.Count * 7919 + 17);
            foreach (var paragraph in pending)
                paragraph.ParagraphId = Next(used, random);
        }

        public static HashSet<string> Collect(WordprocessingDocument document)
        {
            var used = new HashSet<string>(OtherPartIds(document), StringComparer.Ordinal);
            var body = document?.MainDocumentPart?.Document?.Body;
            if (body != null)
            {
                foreach (var paragraph in body.Descendants<Paragraph>())
                {
                    var id = Of(paragraph);
                    if (id != null)
                        used.Add(id);
                }
            }

            return used;
        }

        public static string Assign(Paragraph paragraph, HashSet<string> used)
        {
            var id = Next(used, Random.Shared);
            paragraph.ParagraphId = id;
            return id;
        }

        public static void Renew(Paragraph paragraph)
        {
            if (paragraph.TextId != null)
                paragraph.TextId = Random.Shared.Next(1, int.MaxValue).ToString("X8", CultureInfo.InvariantCulture);
        }

        private static string Next(HashSet<string> used, Random random)
        {
            while (true)
            {
                var id = random.Next(1, int.MaxValue).ToString("X8", CultureInfo.InvariantCulture);
                if (used.Add(id))
                    return id;
            }
        }

        private static void DeclareNamespaces(Document root)
        {
            var w14 = PrefixOf(root, W14Namespace);
            if (w14 == null)
            {
                w14 = FreePrefix(root, "w14");
                root.AddNamespaceDeclaration(w14, W14Namespace);
            }

            if (PrefixOf(root, McNamespace) == null)
                root.AddNamespaceDeclaration(FreePrefix(root, "mc"), McNamespace);

            var tokens = (root.MCAttributes?.Ignorable?.Value ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            if (tokens.Contains(w14))
                return;

            tokens.Add(w14);
            root.MCAttributes ??= new MarkupCompatibilityAttributes();
            root.MCAttributes.Ignorable = string.Join(" ", tokens);
        }

        private static string PrefixOf(OpenXmlElement element, string uri) =>
            element.NamespaceDeclarations.Where(d => d.Value == uri).Select(d => d.Key).FirstOrDefault();

        private static string FreePrefix(OpenXmlElement element, string preferred)
        {
            var prefix = preferred;
            for (var i = 2; element.NamespaceDeclarations.Any(d => d.Key == prefix); i++)
                prefix = preferred + i.ToString(CultureInfo.InvariantCulture);

            return prefix;
        }

        private static IEnumerable<string> OtherPartIds(WordprocessingDocument document)
        {
            var main = document?.MainDocumentPart;
            if (main == null)
                return Array.Empty<string>();

            var ids = new List<string>();
            var parts = new List<OpenXmlPart>();
            parts.AddRange(main.HeaderParts);
            parts.AddRange(main.FooterParts);
            if (main.FootnotesPart != null)
                parts.Add(main.FootnotesPart);

            if (main.EndnotesPart != null)
                parts.Add(main.EndnotesPart);

            if (main.WordprocessingCommentsPart != null)
                parts.Add(main.WordprocessingCommentsPart);

            foreach (var part in parts)
            {
                try
                {
                    var root = part.RootElement;
                    if (root == null)
                        continue;

                    foreach (var paragraph in root.Descendants<Paragraph>())
                    {
                        var id = Of(paragraph);
                        if (id != null)
                            ids.Add(id);
                    }
                }
                catch (Exception)
                {
                }
            }

            return ids;
        }
    }
}
