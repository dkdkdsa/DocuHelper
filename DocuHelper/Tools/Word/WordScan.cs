using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class WordScan
    {
        private readonly List<WordBlock> _blocks = new();
        private readonly Dictionary<string, WordBlock> _byId = new(StringComparer.Ordinal);

        private WordScan(WordprocessingDocument document)
        {
            Document = document;
            Body = document?.MainDocumentPart?.Document?.Body;
            Styles = new WordStyles(document);
            Numbering = new WordNumbering(document, Styles);
        }

        public WordprocessingDocument Document { get; }

        public Body Body { get; }

        public WordStyles Styles { get; }

        public WordNumbering Numbering { get; }

        public IReadOnlyList<WordBlock> Blocks => _blocks;

        public int PageCount { get; private set; } = 1;

        public static WordScan Read(WordprocessingDocument document)
        {
            var scan = new WordScan(document);
            scan.Build();
            return scan;
        }

        public static IEnumerable<OpenXmlElement> BlockElements(OpenXmlElement container)
        {
            foreach (var child in container.ChildElements)
            {
                switch (child)
                {
                    case Paragraph:
                    case Table:
                        yield return child;
                        break;
                    case SdtBlock sdt when sdt.SdtContentBlock != null:
                        foreach (var nested in BlockElements(sdt.SdtContentBlock))
                            yield return nested;
                        break;
                    case CustomXmlBlock custom:
                        foreach (var nested in BlockElements(custom))
                            yield return nested;
                        break;
                }
            }
        }

        public static string PageLabel(IEnumerable<WordBlock> blocks)
        {
            var pages = blocks.Where(b => b != null).Select(b => b.Page).ToList();
            if (pages.Count == 0)
                return null;

            var first = pages.Min();
            var last = pages.Max();
            return first == last ? $"{first}쪽" : $"{first}~{last}쪽";
        }

        public WordBlock Find(string id)
        {
            var key = WordIds.Normalize(id);
            return key != null && _byId.TryGetValue(key, out var block) ? block : null;
        }

        public WordBlock Before(WordBlock block) => block != null && block.Index > 1 ? _blocks[block.Index - 2] : null;

        public WordBlock After(WordBlock block) => block != null && block.Index < _blocks.Count ? _blocks[block.Index] : null;

        public WordBlock Of(OpenXmlElement element) => _blocks.FirstOrDefault(b => ReferenceEquals(b.Element, element));

        private void Build()
        {
            if (Body == null)
                return;

            var pager = new WordPager(Body, Styles.NormalFontSize);
            var sections = Body.Descendants<SectionProperties>().ToList();
            foreach (var element in BlockElements(Body))
            {
                var block = new WordBlock { Index = _blocks.Count + 1 };
                if (element is Table table)
                {
                    block.Table = table;
                    block.Id = WordTables.IdOf(table);
                    block.Kind = WordKind.Table;
                    pager.PlaceTable(block, WordTables.Grid(table));
                }
                else
                {
                    var paragraph = (Paragraph)element;
                    var marked = WordText.Marked(paragraph);
                    block.Paragraph = paragraph;
                    block.Id = WordIds.Of(paragraph);
                    block.Text = Strip(marked);
                    block.Kind = KindOf(paragraph, out var level, out var numbered);
                    block.ListLevel = level;
                    block.IsNumbered = numbered;
                    pager.PlaceParagraph(block, marked, BreaksBefore(paragraph), BreaksAfter(paragraph, sections));
                }

                _blocks.Add(block);
                if (block.Id != null)
                    _byId.TryAdd(block.Id, block);
            }

            PageCount = Math.Max(pager.LastPage, _blocks.Count == 0 ? 1 : _blocks.Max(b => b.Page));
        }

        private WordKind KindOf(Paragraph paragraph, out int level, out bool numbered)
        {
            var list = Numbering.ListKind(paragraph, out level);
            numbered = list != WordKind.Body;
            return Styles.HeadingKind(paragraph) ?? list;
        }

        private bool BreaksBefore(Paragraph paragraph) => WordStyles.IsOn(Styles.ParagraphValue<PageBreakBefore>(paragraph));

        private static bool BreaksAfter(Paragraph paragraph, List<SectionProperties> sections)
        {
            var section = paragraph.ParagraphProperties?.SectionProperties;
            if (section == null)
                return false;

            var index = sections.IndexOf(section);
            var next = index >= 0 && index + 1 < sections.Count ? sections[index + 1] : null;
            var type = next?.GetFirstChild<SectionType>()?.Val;
            return type == null || (type.Value != SectionMarkValues.Continuous && type.Value != SectionMarkValues.NextColumn);
        }

        private static string Strip(string marked)
        {
            if (marked.IndexOf(WordText.PageBreakMark) < 0 && marked.IndexOf(WordText.RenderedBreakMark) < 0)
                return marked;

            return marked.Replace(WordText.PageBreakMark.ToString(), string.Empty).Replace(WordText.RenderedBreakMark.ToString(), string.Empty);
        }
    }
}
