using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class WordPager
    {
        public const int DefaultTextWidth = 9026;
        public const int DefaultTextHeight = 13697;
        public const double LinesPerPage = 1700.0 / 45;

        private readonly WordLayoutContext _context;
        private readonly List<SectionProperties> _marks;
        private int _current;
        private double _used;
        private bool _fresh = true;

        public WordPager(WordLayoutContext context, Body body)
        {
            _context = context;
            _marks = body?.Descendants<SectionProperties>().ToList() ?? new List<SectionProperties>();
            Sections = WordSectionLayout.Build(context, _marks);
        }

        public IReadOnlyList<WordSectionLayout> Sections { get; }

        public int Page { get; private set; } = 1;

        public WordPageLayout Layout => Sections[^1].Page;

        public int LastPage => _fresh && Page > 1 ? Page - 1 : Page;

        public void Place(WordBlock block)
        {
            var section = Sections[_current];
            if (block.IsTable)
                PlaceTable(block, section);
            else
                PlaceParagraph(block, section);

            var mark = block.Paragraph?.ParagraphProperties?.SectionProperties;
            var index = mark == null ? -1 : _marks.IndexOf(mark);
            if (index >= 0 && index + 1 < Sections.Count)
                Transition(index + 1);
        }

        public static void TextArea(Body body, out int width, out int height)
        {
            var layout = LayoutOf(body, 20, 0);
            width = layout.Width - layout.Left - layout.Right;
            height = layout.Height - layout.Top - layout.Bottom;
            if (width < 1440)
                width = DefaultTextWidth;

            if (height < 1440)
                height = DefaultTextHeight;
        }

        public static WordPageLayout LayoutOf(Body body, int normalFontSize, double capacity)
        {
            var section = body?.Elements<SectionProperties>().LastOrDefault();
            var size = section?.GetFirstChild<PageSize>();
            var margin = section?.GetFirstChild<PageMargin>();
            return new WordPageLayout(
                Positive(Twips(size?.Width, 11906), 11906),
                Positive(Twips(size?.Height, 16838), 16838),
                Math.Abs(Twips(margin?.Top, 1701)),
                Math.Abs(Twips(margin?.Right, 1440)),
                Math.Abs(Twips(margin?.Bottom, 1440)),
                Math.Abs(Twips(margin?.Left, 1440)),
                capacity,
                normalFontSize);
        }

        public static double Width(string text)
        {
            var width = 0.0;
            foreach (var c in text)
                width += c >= (char)0x1100 ? 1.0 : c == ' ' ? 0.35 : 0.55;

            return width;
        }

        private static int Positive(int value, int fallback) => value > 0 ? value : fallback;

        private static int Twips(OpenXmlSimpleType value, int fallback) =>
            value != null && double.TryParse(value.InnerText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) && Math.Abs(number) < int.MaxValue
                ? (int)Math.Round(number)
                : fallback;

        private void PlaceParagraph(WordBlock block, WordSectionLayout section)
        {
            var box = WordParagraphMetrics.Measure(_context, block.Paragraph, section.TextWidthPt);
            if (box.PageBefore)
                NewPage(false);

            var spacing = _fresh ? 0 : box.Before;
            block.LineHeight = box.Lines.Select(l => l.Height).FirstOrDefault(h => h > 0);
            block.FontSize = WordParagraphMetrics.FontSizeOf(_context, block.Paragraph);
            var placed = false;
            foreach (var line in box.Lines)
            {
                var extra = placed ? 0 : spacing;
                if (!_fresh && _used + extra + line.Height > section.CapacityPt)
                    NewPage(false);

                if (!placed)
                {
                    block.Page = Page;
                    placed = true;
                    if (!_fresh && spacing > 0)
                    {
                        _used += spacing;
                        block.SpaceBefore = spacing;
                        Add(block, spacing);
                    }
                }

                if (line.Height > 0)
                {
                    _used += line.Height;
                    _fresh = false;
                    Add(block, line.Height);
                }

                if (line.PageBreak)
                    NewPage(true);
            }

            if (!placed)
                block.Page = Page;

            if (!_fresh)
            {
                _used += box.After;
                Add(block, box.After);
            }
        }

        private void PlaceTable(WordBlock block, WordSectionLayout section)
        {
            var rows = WordTableMetrics.RowHeights(_context, block.Table, section.TextWidthPt);
            block.RowHeights.AddRange(rows);
            block.LineHeight = WordFontMetrics.For(_context.LatinFamily(null, null)).LineHeight * _context.NormalSizePt;
            var placed = false;
            foreach (var height in rows)
            {
                if (!_fresh && _used + height > section.CapacityPt)
                    NewPage(false);

                if (!placed)
                {
                    block.Page = Page;
                    placed = true;
                }

                if (height > 0)
                {
                    _used += height;
                    _fresh = false;
                    Add(block, height);
                }
            }

            if (!placed)
                block.Page = Page;
        }

        private void Add(WordBlock block, double height)
        {
            if (height <= 0)
                return;

            if (block.Fragments.Count > 0 && block.Fragments[^1].Page == Page)
            {
                var last = block.Fragments[^1];
                block.Fragments[^1] = last with { Height = last.Height + height };
                return;
            }

            var offset = block.Fragments.Count == 0 ? 0 : block.Fragments[^1].Offset + block.Fragments[^1].Height;
            block.Fragments.Add(new WordFragment(Page, offset, height));
        }

        private void Transition(int next)
        {
            _current = next;
            var start = Sections[next].Start;
            if (start == SectionMarkValues.Continuous)
                return;

            NewPage(false);
            if ((start == SectionMarkValues.EvenPage && Page % 2 != 0) || (start == SectionMarkValues.OddPage && Page % 2 == 0))
                NewPage(true);
        }

        private void NewPage(bool always)
        {
            if (_fresh && !always)
                return;

            Page++;
            _used = 0;
            _fresh = true;
        }
    }
}
