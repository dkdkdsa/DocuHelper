using System;

namespace DocuHelper.Tools.Ppt
{
    public static class PptFit
    {
        private const double EmuPerPoint = 12700;
        private const double LineHeight = 1.2;
        private const double Tolerance = 1.02;

        public static PptFitResult Estimate(PptShapeInfo shape, long slideHeight)
        {
            var result = new PptFitResult();
            if (shape == null || shape.Width <= 0 || shape.Height <= 0 || shape.Paragraphs.Count == 0)
                return result;

            result.AutoShrink = shape.Autofit == PptAutofit.Shrink;
            var width = (shape.Width - shape.LeftInset - shape.RightInset) / EmuPerPoint;
            var boxHeight = shape.Autofit == PptAutofit.Resize && slideHeight > shape.Y
                ? slideHeight - shape.Y
                : shape.Height;
            var height = (boxHeight - shape.TopInset - shape.BottomInset) / EmuPerPoint;
            if (width <= 1 || height <= 1)
                return result;

            double used = 0;
            double firstLine = 0;
            var tooWide = false;
            for (var i = 0; i < shape.Paragraphs.Count; i++)
            {
                var paragraph = shape.Paragraphs[i];
                var size = paragraph.FontSize > 0 ? paragraph.FontSize : 18;
                var lineHeight = paragraph.LineSpacingPoints ?? size * LineHeight * paragraph.LineSpacing;
                if (firstLine <= 0)
                    firstLine = lineHeight;

                var available = Math.Max(size, width - paragraph.LeftMargin);
                int lines;
                if (shape.NoWrap)
                {
                    lines = 0;
                    foreach (var segment in paragraph.Text.Split('\n'))
                    {
                        lines++;
                        if (Measure(segment) * size > available)
                            tooWide = true;
                    }
                }
                else
                {
                    lines = Wrap(paragraph.Text, size, available);
                }

                used += (i > 0 ? paragraph.SpaceBefore : 0) + lines * lineHeight;
            }

            if (firstLine <= 0)
                return result;

            result.IsKnown = true;
            result.FontSize = shape.First?.FontSize ?? 18;
            result.Ratio = used / height;
            result.TooWide = tooWide;
            result.Fits = !tooWide && used <= height * Tolerance;
            result.LinesNeeded = Math.Max(1, (int)Math.Ceiling(used / firstLine - 0.05));
            result.LinesAvailable = Math.Max(1, (int)Math.Floor(height / firstLine + 0.05));
            if (result.Fits)
                result.LinesNeeded = Math.Min(result.LinesNeeded, result.LinesAvailable);
            else if (!tooWide && result.LinesNeeded <= result.LinesAvailable)
                result.LinesNeeded = result.LinesAvailable + 1;

            return result;
        }

        public static double EmWidth(char ch)
        {
            if (ch == ' ' || ch == (char)0xA0 || ch == '\t')
                return 0.3;

            if (IsWide(ch))
                return 1.0;

            if (".,:;'|!`il".IndexOf(ch) >= 0)
                return 0.3;

            return 0.55;
        }

        public static double Measure(string text)
        {
            double width = 0;
            foreach (var ch in text ?? string.Empty)
                width += EmWidth(ch);

            return width;
        }

        private static bool IsWide(char ch) =>
            (ch >= 0x1100 && ch <= 0x11FF)
            || (ch >= 0x2190 && ch <= 0x21FF)
            || (ch >= 0x2460 && ch <= 0x24FF)
            || (ch >= 0x25A0 && ch <= 0x27BF)
            || (ch >= 0x2E80 && ch <= 0x9FFF)
            || (ch >= 0xAC00 && ch <= 0xD7A3)
            || (ch >= 0xF900 && ch <= 0xFAFF)
            || (ch >= 0xFF00 && ch <= 0xFF60)
            || (ch >= 0xFFE0 && ch <= 0xFFE6);

        private static int Wrap(string text, double size, double available)
        {
            var total = 0;
            foreach (var segment in (text ?? string.Empty).Split('\n'))
                total += WrapSegment(segment, size, available);

            return Math.Max(1, total);
        }

        private static int WrapSegment(string text, double size, double available)
        {
            if (string.IsNullOrEmpty(text))
                return 1;

            var lines = 1;
            double x = 0;
            var space = EmWidth(' ') * size;
            foreach (var word in text.Split(' '))
            {
                var width = Measure(word) * size;
                var needed = x > 0 ? space + width : width;
                if (x + needed <= available)
                {
                    x += needed;
                    continue;
                }

                if (x > 0)
                {
                    lines++;
                    x = 0;
                }

                if (width <= available)
                {
                    x = width;
                    continue;
                }

                foreach (var ch in word)
                {
                    var charWidth = EmWidth(ch) * size;
                    if (x > 0 && x + charWidth > available)
                    {
                        lines++;
                        x = 0;
                    }

                    x += charWidth;
                }
            }

            return lines;
        }
    }
}
