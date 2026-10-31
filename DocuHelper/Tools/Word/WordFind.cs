using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public static class WordFind
    {
        public const int MaxQueryLength = 500;
        private const int Context = 20;

        public static IReadOnlyList<WordMatch> Find(WordDocu docu, string query, WordFindOptions options = null)
        {
            if (docu?.Document?.MainDocumentPart?.Document?.Body == null || string.IsNullOrEmpty(query))
                return new List<WordMatch>();

            return Find(docu, WordScan.Read(docu.Document), query, options);
        }

        public static IReadOnlyList<WordMatch> Find(WordDocu docu, WordScan scan, string query, WordFindOptions options = null)
        {
            options ??= new WordFindOptions();
            var found = new List<WordMatch>();
            if (string.IsNullOrEmpty(query))
                return found;

            if (query.Length > MaxQueryLength)
                throw new ToolInputException($"찾을 글은 {MaxQueryLength}자까지만 쓸 수 있어요.");

            var matcher = WordMatcher.Create(query, options);
            foreach (var site in Sites(docu, scan, options))
            {
                var text = WordTextMap.Build(site.Paragraph).Text;
                foreach (var (start, end, _) in matcher.Matches(text))
                {
                    found.Add(new WordMatch(site, start, end, text.Substring(start, end - start), Around(text, Math.Max(0, start - Context), start), Around(text, end, Math.Min(text.Length, end + Context))));
                    if (options.Limit != null && found.Count >= options.Limit.Value)
                        return found;
                }
            }

            return found;
        }

        public static IReadOnlyList<WordFindSite> Sites(WordDocu docu, WordScan scan, WordFindOptions options)
        {
            options ??= new WordFindOptions();
            var sites = new List<WordFindSite>();
            var body = scan.Body;
            if (body == null)
                return sites;

            HashSet<WordBlock> scope = null;
            if (options.Scope != null && options.Scope.Count > 0)
            {
                scope = new HashSet<WordBlock>();
                foreach (var id in options.Scope)
                {
                    var block = scan.Find(id) ?? throw new ToolInputException($"‘{WordIds.Normalize(id) ?? id}’ 문단을 찾지 못했어요. read_paragraphs로 id를 다시 확인해 주세요.");
                    scope.Add(block);
                }
            }

            var owners = new Dictionary<OpenXmlElement, WordBlock>();
            foreach (var block in scan.Blocks)
                owners[block.Element] = block;

            if ((options.Parts & (WordFindParts.Body | WordFindParts.Tables | WordFindParts.TextBoxes)) != 0)
            {
                foreach (var paragraph in body.Descendants<Paragraph>())
                {
                    if (InFallback(paragraph))
                        continue;

                    var part = Classify(paragraph);
                    if ((options.Parts & part) == 0)
                        continue;

                    var owner = OwnerOf(paragraph, owners);
                    if (owner == null || (scope != null && !scope.Contains(owner)))
                        continue;

                    sites.Add(new WordFindSite(paragraph, owner, part, owner.Anchor));
                }
            }

            if (scope != null)
                return sites;

            if ((options.Parts & WordFindParts.HeadersFooters) != 0)
                AddHeadersFooters(docu, sites);

            if ((options.Parts & WordFindParts.Notes) != 0)
                AddNotes(docu, sites);

            return sites;
        }

        private static string Around(string text, int from, int to) =>
            text.Substring(from, to - from).Replace('\n', ' ').Replace('\t', ' ');

        private static bool InFallback(Paragraph paragraph) => paragraph.Ancestors<AlternateContentFallback>().Any();

        private static WordFindParts Classify(Paragraph paragraph)
        {
            if (paragraph.Ancestors<TextBoxContent>().Any())
                return WordFindParts.TextBoxes;

            return paragraph.Ancestors<Table>().Any() ? WordFindParts.Tables : WordFindParts.Body;
        }

        private static WordBlock OwnerOf(Paragraph paragraph, Dictionary<OpenXmlElement, WordBlock> owners)
        {
            for (OpenXmlElement element = paragraph; element != null; element = element.Parent)
            {
                if (owners.TryGetValue(element, out var block))
                    return block;
            }

            return null;
        }

        private static void AddHeadersFooters(WordDocu docu, List<WordFindSite> sites)
        {
            var main = docu.Document.MainDocumentPart;
            var numbers = new Dictionary<OpenXmlPart, int>();
            var sections = WordParts.Sections(docu);
            for (var i = 0; i < sections.Count; i++)
            {
                var ids = sections[i].Elements<HeaderReference>().Select(r => r.Id?.Value).Concat(sections[i].Elements<FooterReference>().Select(r => r.Id?.Value));
                foreach (var id in ids)
                {
                    if (id != null && main.TryGetPartById(id, out var part))
                        numbers.TryAdd(part, i + 1);
                }
            }

            foreach (var part in main.HeaderParts)
                AddStory(sites, part.Header, WordAnchors.HeaderFooter(WordAnchors.Header, numbers.GetValueOrDefault(part, 1)), WordFindParts.HeadersFooters);

            foreach (var part in main.FooterParts)
                AddStory(sites, part.Footer, WordAnchors.HeaderFooter(WordAnchors.Footer, numbers.GetValueOrDefault(part, 1)), WordFindParts.HeadersFooters);
        }

        private static void AddNotes(WordDocu docu, List<WordFindSite> sites)
        {
            var main = docu.Document.MainDocumentPart;
            var footnotes = main.FootnotesPart?.Footnotes?.Elements<Footnote>() ?? Enumerable.Empty<Footnote>();
            foreach (var note in footnotes.Where(n => n.Type == null || n.Type.Value == FootnoteEndnoteValues.Normal))
                AddStory(sites, note, WordAnchors.Note("footnote", (int)(note.Id?.Value ?? 0)), WordFindParts.Notes);

            var endnotes = main.EndnotesPart?.Endnotes?.Elements<Endnote>() ?? Enumerable.Empty<Endnote>();
            foreach (var note in endnotes.Where(n => n.Type == null || n.Type.Value == FootnoteEndnoteValues.Normal))
                AddStory(sites, note, WordAnchors.Note("endnote", (int)(note.Id?.Value ?? 0)), WordFindParts.Notes);
        }

        private static void AddStory(List<WordFindSite> sites, OpenXmlElement root, string anchor, WordFindParts part)
        {
            if (root == null)
                return;

            foreach (var paragraph in root.Descendants<Paragraph>())
            {
                if (!InFallback(paragraph))
                    sites.Add(new WordFindSite(paragraph, null, part, anchor));
            }
        }
    }
}
