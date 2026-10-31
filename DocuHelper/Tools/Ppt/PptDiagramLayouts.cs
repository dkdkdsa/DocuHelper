using System;
using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public static class PptDiagramLayouts
    {
        public const long MinBoxWidth = 432000;

        public const long MinBoxHeight = 252000;

        public const double MinFontPoints = 14;

        public const double MaxFontPoints = 28;

        private const long Cm = PptUnits.EmuPerCm;

        private const long TextInsetX = 91440;

        private const long TextInsetY = 45720;

        private const double LineSpacing = 1.2;

        private const double WrapSlack = 1.05;

        private static readonly int[] Scales = { 90000, 80000, 70000, 60000, 50000 };

        public static PptDiagramPlan Plan(PptDiagramKind kind, int count, PptBox frame)
        {
            PptDiagramKinds.CheckCount(kind, count);
            if (frame.Width <= 0 || frame.Height <= 0)
                throw TooSmall();

            var plan = kind switch
            {
                PptDiagramKind.List => List(count, frame),
                PptDiagramKind.Process => Process(count, frame),
                PptDiagramKind.Cycle => Cycle(count, frame),
                PptDiagramKind.Hierarchy => Hierarchy(count, frame),
                PptDiagramKind.Pyramid => Pyramid(count, frame),
                _ => Matrix(frame),
            };

            foreach (var box in plan.Boxes)
            {
                if (box.Box.Width < MinBoxWidth || box.Box.Height < MinBoxHeight)
                    throw TooSmall();
            }

            return plan;
        }

        public static double FitFontSize(string text, PptBox area)
        {
            for (var size = MaxFontPoints; size >= MinFontPoints; size--)
            {
                if (Fits(text, size, area))
                    return size;
            }

            return 0;
        }

        public static int FitScale(string text, double size, PptBox area)
        {
            if (Fits(text, size, area))
                return 100000;

            foreach (var scale in Scales)
            {
                if (Fits(text, size * scale / 100000.0, area))
                    return scale;
            }

            return Scales[^1];
        }

        public static bool Fits(string text, double size, PptBox area) =>
            Lines(text, size, area.Width) * size * LineSpacing * PptUnits.EmuPerPoint <= area.Height;

        public static int Lines(string text, double size, long width)
        {
            var available = Math.Max(1, width) / (double)PptUnits.EmuPerPoint;
            var total = 0;
            foreach (var segment in (text ?? string.Empty).Split('\n'))
                total += Math.Max(1, (int)Math.Ceiling(PptFit.Measure(segment) * size / available * WrapSlack));

            return total;
        }

        private static PptDiagramPlan List(int count, PptBox frame)
        {
            var gap = Math.Min(Cm / 3, frame.Height / (4L * count));
            var height = Math.Min((frame.Height - (count - 1) * gap) / count, 3 * Cm);
            var top = frame.Y + (frame.Height - (count * height + (count - 1) * gap)) / 2;
            var boxes = new List<PptDiagramBox>();
            for (var i = 0; i < count; i++)
                boxes.Add(Rounded(i, new PptBox(frame.X, top + i * (height + gap), frame.Width, height)));

            return new PptDiagramPlan(boxes, Array.Empty<PptDiagramLink>());
        }

        private static PptDiagramPlan Process(int count, PptBox frame)
        {
            var gap = Math.Clamp((long)(frame.Width * 0.05), 7 * Cm / 10, 15 * Cm / 10);
            var width = (frame.Width - (count - 1) * gap) / count;
            var horizontal = width >= 2 * Cm || frame.Width >= frame.Height;
            var boxes = new List<PptDiagramBox>();
            var links = new List<PptDiagramLink>();
            if (horizontal)
            {
                var height = Math.Min(frame.Height, Math.Clamp((long)(width * 0.8), 16 * Cm / 10, 4 * Cm));
                var top = frame.Y + (frame.Height - height) / 2;
                for (var i = 0; i < count; i++)
                    boxes.Add(Rounded(i, new PptBox(frame.X + i * (width + gap), top, width, height)));

                for (var i = 0; i + 1 < count; i++)
                {
                    var from = boxes[i].Box;
                    var to = boxes[i + 1].Box;
                    links.Add(new PptDiagramLink(i, i + 1, from.Right, top + height / 2, to.X, top + height / 2, true, 3, 1));
                }

                return new PptDiagramPlan(boxes, links);
            }

            gap = Math.Clamp((long)(frame.Height * 0.05), 6 * Cm / 10, 12 * Cm / 10);
            var rowHeight = (frame.Height - (count - 1) * gap) / count;
            var rowWidth = Math.Min(frame.Width, Math.Clamp((long)(rowHeight * 3.5), 4 * Cm, 14 * Cm));
            var left = frame.X + (frame.Width - rowWidth) / 2;
            for (var i = 0; i < count; i++)
                boxes.Add(Rounded(i, new PptBox(left, frame.Y + i * (rowHeight + gap), rowWidth, rowHeight)));

            for (var i = 0; i + 1 < count; i++)
            {
                var from = boxes[i].Box;
                var to = boxes[i + 1].Box;
                links.Add(new PptDiagramLink(i, i + 1, left + rowWidth / 2, from.Bottom, left + rowWidth / 2, to.Y, true, 2, 0));
            }

            return new PptDiagramPlan(boxes, links);
        }

        private static PptDiagramPlan Cycle(int count, PptBox frame)
        {
            var width = Math.Min(Math.Min(frame.Width, frame.Height) * 0.34, 6.0 * Cm);
            var margin = Cm / 4;
            List<PptDiagramBox> boxes = null;
            while (boxes == null)
            {
                if (width < 1.6 * Cm)
                    throw TooSmall();

                var height = Math.Max(width * 0.55, 0.9 * Cm);
                var candidate = new List<PptDiagramBox>();
                var radiusX = (frame.Width - width) / 2;
                var radiusY = (frame.Height - height) / 2;
                for (var i = 0; i < count; i++)
                {
                    var angle = -Math.PI / 2 + 2 * Math.PI * i / count;
                    var centerX = frame.CenterX + radiusX * Math.Cos(angle);
                    var centerY = frame.CenterY + radiusY * Math.Sin(angle);
                    candidate.Add(Rounded(
                        i,
                        new PptBox((long)Math.Round(centerX - width / 2), (long)Math.Round(centerY - height / 2), (long)Math.Round(width), (long)Math.Round(height))));
                }

                if (Separate(candidate, margin))
                    boxes = candidate;
                else
                    width *= 0.94;
            }

            var links = new List<PptDiagramLink>();
            var offset = count == 2 ? 0.3 * Cm : 0;
            for (var i = 0; i < count; i++)
            {
                var from = boxes[i].Box;
                var to = boxes[(i + 1) % count].Box;
                var start = Edge(from, to.CenterX, to.CenterY, Cm / 12.0);
                var end = Edge(to, from.CenterX, from.CenterY, Cm / 12.0);
                var dx = end.X - start.X;
                var dy = end.Y - start.Y;
                var length = Math.Sqrt((double)dx * dx + (double)dy * dy);
                if (length < Cm / 5.0)
                    continue;

                var shiftX = (long)Math.Round(-dy / length * offset);
                var shiftY = (long)Math.Round(dx / length * offset);
                links.Add(new PptDiagramLink(i, (i + 1) % count, start.X + shiftX, start.Y + shiftY, end.X + shiftX, end.Y + shiftY, true, -1, -1));
            }

            return new PptDiagramPlan(boxes, links);
        }

        private static PptDiagramPlan Hierarchy(int count, PptBox frame)
        {
            var children = count - 1;
            var gapV = Math.Clamp((long)(frame.Height * 0.22), 9 * Cm / 10, 25 * Cm / 10);
            var rowHeight = Math.Min(3 * Cm, (frame.Height - gapV) / 2);
            var gapH = Math.Clamp((long)(frame.Width * 0.03), 3 * Cm / 10, 8 * Cm / 10);
            var width = Math.Min((frame.Width - (children - 1) * gapH) / children, 7 * Cm);
            var rowWidth = children * width + (children - 1) * gapH;
            var left = frame.X + (frame.Width - rowWidth) / 2;
            var top = frame.Y + (frame.Height - (2 * rowHeight + gapV)) / 2;
            var rootWidth = Math.Min(rowWidth, Math.Max((long)(width * 1.3), 4 * Cm));
            var boxes = new List<PptDiagramBox>
            {
                Rounded(0, new PptBox((long)Math.Round(frame.CenterX - rootWidth / 2.0), top, rootWidth, rowHeight)),
            };

            var links = new List<PptDiagramLink>();
            var rootBottom = top + rowHeight;
            var childTop = rootBottom + gapV;
            for (var i = 0; i < children; i++)
            {
                var box = Rounded(i + 1, new PptBox(left + i * (width + gapH), childTop, width, rowHeight));
                boxes.Add(box);
                links.Add(new PptDiagramLink(0, i + 1, (long)Math.Round(frame.CenterX), rootBottom, (long)Math.Round(box.Box.CenterX), childTop, false, 2, 0));
            }

            return new PptDiagramPlan(boxes, links);
        }

        private static PptDiagramPlan Pyramid(int count, PptBox frame)
        {
            var gap = Cm / 8;
            var baseWidth = Math.Min(frame.Width, 2 * frame.Height);
            var height = (frame.Height - (count - 1) * gap) / count;
            var boxes = new List<PptDiagramBox>();
            for (var i = 0; i < count; i++)
            {
                var topWidth = baseWidth * (0.12 + 0.88 * i / count);
                var bottomWidth = baseWidth * (0.12 + 0.88 * (i + 1) / count);
                var width = (long)Math.Round(bottomWidth);
                var inset = (bottomWidth - topWidth) / 2;
                var side = Math.Min(width, height);
                var maxAdjust = 50000.0 * width / side;
                var adjust = Math.Min(inset / side * 100000, maxAdjust);
                var ratio = maxAdjust <= 0 ? 0 : adjust / maxAdjust;
                var x = (long)Math.Round(frame.CenterX - width / 2.0);
                var y = frame.Y + i * (height + gap);
                var textWidth = Math.Max(1, (long)Math.Round(width - 4.0 / 3 * inset - 2 * TextInsetX));
                var textTop = (long)Math.Round(height / 3.0 * ratio) + TextInsetY;
                var textHeight = Math.Max(1, height - textTop - TextInsetY);
                boxes.Add(new PptDiagramBox(
                    i,
                    "trapezoid",
                    new PptBox(x, y, width, height),
                    (int)Math.Round(adjust),
                    new PptBox((long)Math.Round(frame.CenterX - textWidth / 2.0), y + textTop, textWidth, textHeight)));
            }

            return new PptDiagramPlan(boxes, Array.Empty<PptDiagramLink>());
        }

        private static PptDiagramPlan Matrix(PptBox frame)
        {
            var gap = 3 * Cm / 10;
            var width = Math.Min((frame.Width - gap) / 2, 14 * Cm);
            var height = Math.Min((frame.Height - gap) / 2, 6 * Cm);
            var left = frame.X + (frame.Width - (2 * width + gap)) / 2;
            var top = frame.Y + (frame.Height - (2 * height + gap)) / 2;
            var boxes = new List<PptDiagramBox>();
            for (var i = 0; i < 4; i++)
                boxes.Add(Plain(i, "rect", new PptBox(left + i % 2 * (width + gap), top + i / 2 * (height + gap), width, height)));

            return new PptDiagramPlan(boxes, Array.Empty<PptDiagramLink>());
        }

        private static PptDiagramBox Rounded(int index, PptBox box) => Plain(index, "roundRect", box);

        private static PptDiagramBox Plain(int index, string preset, PptBox box)
        {
            var area = box.Inset(TextInsetX, TextInsetY, TextInsetX, TextInsetY);
            if (area.Width < 1 || area.Height < 1)
                area = box;

            return new PptDiagramBox(index, preset, box, null, area);
        }

        private static bool Separate(List<PptDiagramBox> boxes, long margin)
        {
            for (var i = 0; i < boxes.Count; i++)
            {
                var inflated = boxes[i].Box.Inset(-margin, -margin, -margin, -margin);
                for (var j = i + 1; j < boxes.Count; j++)
                {
                    if (inflated.Intersects(boxes[j].Box))
                        return false;
                }
            }

            return true;
        }

        private static (long X, long Y) Edge(PptBox box, double towardX, double towardY, double margin)
        {
            var dx = towardX - box.CenterX;
            var dy = towardY - box.CenterY;
            var halfWidth = box.Width / 2.0 + margin;
            var halfHeight = box.Height / 2.0 + margin;
            var scale = Math.Min(dx == 0 ? double.MaxValue : halfWidth / Math.Abs(dx), dy == 0 ? double.MaxValue : halfHeight / Math.Abs(dy));
            return ((long)Math.Round(box.CenterX + dx * scale), (long)Math.Round(box.CenterY + dy * scale));
        }

        private static ToolInputException TooSmall() =>
            new("도형을 그릴 영역이 너무 작아요. 영역(w, h)을 키우거나 항목 수를 줄여 주세요.");
    }
}
