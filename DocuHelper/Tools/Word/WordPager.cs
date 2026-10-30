using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class WordPager
    {
        public const int DefaultTextWidth = 9026;
        public const int DefaultTextHeight = 13697;

        private const double CharsPerLine = 45;
        private const double LinesPerPage = 1700.0 / 45;

        private readonly double _charsPerLine;
        private readonly double _linesPerPage;
        private readonly double _limit;
        private double _used;
        private bool _fresh = true;

        public WordPager(Body body, int normalFontSize)
        {
            TextArea(body, out var width, out var height);
            var font = 20.0 / Math.Max(10, normalFontSize);
            _charsPerLine = Math.Max(10, CharsPerLine * width / DefaultTextWidth * font);
            _linesPerPage = Math.Max(10, LinesPerPage * height / DefaultTextHeight * font);
            _limit = body != null && body.Descendants<LastRenderedPageBreak>().Any() ? _linesPerPage * 1.3 : _linesPerPage;
        }

        public int Page { get; private set; } = 1;

        public int LastPage => _fresh && Page > 1 ? Page - 1 : Page;

        public static void TextArea(Body body, out int width, out int height)
        {
            var section = body?.Elements<SectionProperties>().LastOrDefault();
            var size = section?.GetFirstChild<PageSize>();
            var margin = section?.GetFirstChild<PageMargin>();
            var pageWidth = (int)(size?.Width?.Value ?? 11906U);
            var pageHeight = (int)(size?.Height?.Value ?? 16838U);
            width = pageWidth - (int)(margin?.Left?.Value ?? 1440U) - (int)(margin?.Right?.Value ?? 1440U);
            height = pageHeight - Math.Abs(margin?.Top?.Value ?? 1701) - Math.Abs(margin?.Bottom?.Value ?? 1440);
            if (width < 1440)
                width = DefaultTextWidth;

            if (height < 1440)
                height = DefaultTextHeight;
        }

        public void PlaceParagraph(WordBlock block, string marked, bool breakBefore, bool breakAfter)
        {
            if (breakBefore)
                Break(false);

            var segments = Split(marked);
            var hasText = segments.Any(s => s.Text.Length > 0);
            var index = 0;
            if (hasText)
            {
                while (index < segments.Count - 1 && segments[index].Text.Length == 0)
                {
                    Break(segments[index].Mark == WordText.PageBreakMark);
                    index++;
                }
            }

            var height = Height(block.Kind);
            var spacing = Spacing(block.Kind);
            if (!_fresh)
            {
                var first = Lines(segments[index].Text, _charsPerLine) * height;
                var needed = block.IsHeading ? first + spacing + 2 : Math.Min(first, 1);
                if (_used + needed > _limit)
                    Break(false);
            }

            block.Page = Page;
            for (var start = index; index < segments.Count; index++)
            {
                if (index == start || segments[index].Text.Length > 0)
                    Add(Lines(segments[index].Text, _charsPerLine) * height);

                if (index < segments.Count - 1)
                    Break(segments[index].Mark == WordText.PageBreakMark);
            }

            if (!_fresh)
                Add(spacing);

            if (breakAfter)
                Break(false);
        }

        public void PlaceTable(WordBlock block, List<List<WordCell>> grid)
        {
            var columns = Math.Max(1, WordTables.ColumnCount(grid));
            var cellChars = Math.Max(4, _charsPerLine / columns - 2);
            var placed = false;
            foreach (var row in grid)
            {
                var owners = row.Where(c => c.IsOrigin && c.Owner != null).Select(c => c.Owner).ToList();
                if (owners.Any(o => o.Descendants<LastRenderedPageBreak>().Any()))
                    Break(false);

                if (!placed)
                {
                    if (!_fresh && _used + 1.5 > _limit)
                        Break(false);

                    block.Page = Page;
                    placed = true;
                }

                var lines = 1.0;
                foreach (var owner in owners)
                {
                    var count = owner.Elements<Paragraph>().Sum(p => Lines(WordText.Of(p), cellChars));
                    lines = Math.Max(lines, count);
                }

                Add(lines + 0.3);
            }

            if (!placed)
                block.Page = Page;

            Add(0.6);
        }

        public static double Width(string text)
        {
            var width = 0.0;
            foreach (var c in text)
                width += c >= (char)0x1100 ? 1.0 : c == ' ' ? 0.35 : 0.55;

            return width;
        }

        private static double Lines(string text, double charsPerLine)
        {
            var lines = 0.0;
            foreach (var line in text.Split('\n'))
                lines += Math.Max(1, Math.Ceiling(Width(line) / charsPerLine));

            return lines;
        }

        private static double Height(WordKind kind) => kind switch
        {
            WordKind.Title => 2.0,
            WordKind.Heading1 => 1.5,
            WordKind.Heading2 => 1.3,
            _ => 1.0,
        };

        private static double Spacing(WordKind kind) => kind switch
        {
            WordKind.Title => 1.2,
            WordKind.Heading1 => 1.0,
            WordKind.Heading2 => 0.8,
            _ => 0.35,
        };

        private static List<(string Text, char Mark)> Split(string marked)
        {
            var segments = new List<(string Text, char Mark)>();
            var start = 0;
            for (var i = 0; i < marked.Length; i++)
            {
                var c = marked[i];
                if (c != WordText.PageBreakMark && c != WordText.RenderedBreakMark)
                    continue;

                segments.Add((marked.Substring(start, i - start), c));
                start = i + 1;
            }

            segments.Add((marked.Substring(start), '\0'));
            return segments;
        }

        private void Add(double lines)
        {
            if (lines <= 0)
                return;

            _used += lines;
            _fresh = false;
            while (_used > _limit)
            {
                Page++;
                _used -= _linesPerPage;
            }
        }

        private void Break(bool always)
        {
            if (_fresh && !always)
                return;

            Page++;
            _used = 0;
            _fresh = true;
        }
    }
}
