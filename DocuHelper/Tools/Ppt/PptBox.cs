using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Ppt
{
    public readonly record struct PptBox(long X, long Y, long Width, long Height)
    {
        public long Right => X + Width;

        public long Bottom => Y + Height;

        public double CenterX => X + Width / 2.0;

        public double CenterY => Y + Height / 2.0;

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public static PptBox FromCm(double x, double y, double width, double height) =>
            new(PptUnits.ToEmu(x), PptUnits.ToEmu(y), PptUnits.ToEmu(width), PptUnits.ToEmu(height));

        public static PptBox Bounds(IEnumerable<PptBox> boxes)
        {
            var list = boxes.ToList();
            if (list.Count == 0)
                return default;

            var left = list.Min(b => b.X);
            var top = list.Min(b => b.Y);
            var right = list.Max(b => b.Right);
            var bottom = list.Max(b => b.Bottom);
            return new PptBox(left, top, right - left, bottom - top);
        }

        public PptBox Offset(long dx, long dy) => new(X + dx, Y + dy, Width, Height);

        public PptBox MoveTo(long x, long y) => new(x, y, Width, Height);

        public PptBox Resize(long width, long height) => new(X, Y, width, height);

        public PptBox Inset(long left, long top, long right, long bottom) =>
            new(X + left, Y + top, Width - left - right, Height - top - bottom);

        public PptBox CenteredIn(PptBox area) =>
            new((long)Math.Round(area.CenterX - Width / 2.0), (long)Math.Round(area.CenterY - Height / 2.0), Width, Height);

        public PptBox Union(PptBox other) => Bounds(new[] { this, other });

        public bool Contains(PptBox other) =>
            other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

        public bool Intersects(PptBox other) =>
            X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

        public PptBox Rotated(double degrees)
        {
            var radians = PptUnits.NormalizeDegrees(degrees) * Math.PI / 180;
            var cos = Math.Abs(Math.Cos(radians));
            var sin = Math.Abs(Math.Sin(radians));
            var width = (long)Math.Round(Width * cos + Height * sin);
            var height = (long)Math.Round(Width * sin + Height * cos);
            return new PptBox((long)Math.Round(CenterX - width / 2.0), (long)Math.Round(CenterY - height / 2.0), width, height);
        }

        public PptBox Scaled(double scaleX, double scaleY) =>
            new(
                (long)Math.Round(X * scaleX),
                (long)Math.Round(Y * scaleY),
                (long)Math.Round(Width * scaleX),
                (long)Math.Round(Height * scaleY));

        public PptBox Fit(long contentWidth, long contentHeight, double fraction = 1, bool allowEnlarge = false)
        {
            if (contentWidth <= 0 || contentHeight <= 0)
                return new PptBox((long)Math.Round(CenterX), (long)Math.Round(CenterY), 0, 0);

            var scale = Math.Min(Width * fraction / contentWidth, Height * fraction / contentHeight);
            if (!allowEnlarge)
                scale = Math.Min(scale, 1);

            var width = Math.Max(1, (long)Math.Round(contentWidth * scale));
            var height = Math.Max(1, (long)Math.Round(contentHeight * scale));
            return new PptBox(0, 0, width, height).CenteredIn(this);
        }
    }
}
