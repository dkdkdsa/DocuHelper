using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocuHelper.Tools.Excel
{
    public static class SqrefMath
    {
        public const int MaxAreas = 10000;

        private static readonly Regex s_colon = new(@"\s*:\s*", RegexOptions.CultureInvariant);
        private static readonly Regex s_separator = new(@"[\s,;]+", RegexOptions.CultureInvariant);
        private static readonly Comparer<CellRange> s_byLeft = Comparer<CellRange>.Create((x, y) => x.Left.CompareTo(y.Left));

        public static bool TryParse(string sqref, out IReadOnlyList<CellRange> ranges)
        {
            var valid = Scan(sqref, out var list, out _);
            ranges = list;
            return valid;
        }

        public static IReadOnlyList<CellRange> Parse(string sqref)
        {
            if (!Scan(sqref, out var list, out var problem))
                throw new ToolInputException(problem);

            return list;
        }

        public static string Format(IEnumerable<CellRange> ranges)
        {
            var parts = (ranges ?? Enumerable.Empty<CellRange>()).Where(r => r != null).Select(r => r.ToString()).ToList();
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        public static IReadOnlyList<CellRange> Normalize(IEnumerable<CellRange> ranges) => Combine(ranges, null, Operation.Union);

        public static string Normalize(string sqref) => TryParse(sqref, out var ranges) ? Format(Normalize(ranges)) : sqref;

        public static IReadOnlyList<CellRange> Union(IEnumerable<CellRange> first, IEnumerable<CellRange> second) => Combine(first, second, Operation.Union);

        public static IReadOnlyList<CellRange> Union(IEnumerable<CellRange> first, CellRange second) => Combine(first, Single(second), Operation.Union);

        public static IReadOnlyList<CellRange> Intersect(IEnumerable<CellRange> first, IEnumerable<CellRange> second) => Combine(first, second, Operation.Intersect);

        public static IReadOnlyList<CellRange> Intersect(IEnumerable<CellRange> first, CellRange second) => Combine(first, Single(second), Operation.Intersect);

        public static string Intersect(string sqref, IEnumerable<CellRange> keep) => TryParse(sqref, out var ranges) ? Format(Intersect(ranges, keep)) : null;

        public static string Intersect(string sqref, CellRange keep) => Intersect(sqref, Single(keep));

        public static IReadOnlyList<CellRange> Subtract(IEnumerable<CellRange> source, IEnumerable<CellRange> removed) => Combine(source, removed, Operation.Subtract);

        public static IReadOnlyList<CellRange> Subtract(IEnumerable<CellRange> source, CellRange removed) => Combine(source, Single(removed), Operation.Subtract);

        public static IReadOnlyList<CellRange> Subtract(CellRange source, IEnumerable<CellRange> removed) => Combine(Single(source), removed, Operation.Subtract);

        public static IReadOnlyList<CellRange> Subtract(CellRange source, CellRange removed) => Combine(Single(source), Single(removed), Operation.Subtract);

        public static string Subtract(string sqref, IEnumerable<CellRange> removed)
        {
            var cut = (removed ?? Enumerable.Empty<CellRange>()).Where(r => r != null).ToList();
            if (!TryParse(sqref, out var ranges) || !Intersects(ranges, cut))
                return sqref;

            return Format(Subtract(ranges, cut));
        }

        public static string Subtract(string sqref, CellRange removed) => Subtract(sqref, Single(removed));

        public static bool Contains(IEnumerable<CellRange> ranges, int column, int row) =>
            ranges != null && ranges.Any(r => r != null && r.Contains(column, row));

        public static bool Covers(IEnumerable<CellRange> ranges, CellRange target) =>
            target == null || Subtract(target, ranges).Count == 0;

        public static bool Intersects(IEnumerable<CellRange> first, IEnumerable<CellRange> second)
        {
            if (first == null || second == null)
                return false;

            var others = second.Where(r => r != null).ToList();
            return first.Any(a => a != null && others.Any(a.Intersects));
        }

        public static bool Intersects(IEnumerable<CellRange> ranges, CellRange range) => Intersects(ranges, Single(range));

        public static bool SameCells(IEnumerable<CellRange> first, IEnumerable<CellRange> second) =>
            Subtract(first, second).Count == 0 && Subtract(second, first).Count == 0;

        public static long CellCount(IEnumerable<CellRange> ranges) => Normalize(ranges).Sum(r => r.CellCount);

        public static CellRange Bounds(IEnumerable<CellRange> ranges)
        {
            CellRange bounds = null;
            foreach (var range in ranges ?? Enumerable.Empty<CellRange>())
            {
                if (range != null)
                    bounds = bounds == null ? range : bounds.Union(range);
            }

            return bounds;
        }

        public static IEnumerable<CellAddress> Cells(IEnumerable<CellRange> ranges) => Normalize(ranges).SelectMany(r => r.Cells());

        public static IReadOnlyList<CellRange> Translate(IEnumerable<CellRange> ranges, int columnDelta, int rowDelta)
        {
            var moved = new List<CellRange>();
            foreach (var range in ranges ?? Enumerable.Empty<CellRange>())
            {
                if (range == null)
                    continue;

                var left = Math.Max(0L, (long)range.Left + columnDelta);
                var right = Math.Min(CellAddress.MaxColumns - 1L, (long)range.Right + columnDelta);
                var top = Math.Max(0L, (long)range.Top + rowDelta);
                var bottom = Math.Min(CellAddress.MaxRows - 1L, (long)range.Bottom + rowDelta);
                if (left <= right && top <= bottom)
                    moved.Add(new CellRange((int)left, (int)top, (int)right, (int)bottom));
            }

            return moved;
        }

        private static IEnumerable<CellRange> Single(CellRange range) => range == null ? Array.Empty<CellRange>() : new[] { range };

        private static bool Scan(string text, out List<CellRange> ranges, out string problem)
        {
            ranges = new List<CellRange>();
            problem = null;
            var tokens = string.IsNullOrWhiteSpace(text)
                ? new List<string>()
                : s_separator.Split(s_colon.Replace(text.Trim(), ":")).Where(t => t.Length > 0).ToList();
            if (tokens.Count == 0)
            {
                problem = "범위가 비어 있어요. B2:D10처럼 알려 주세요.";
                return false;
            }

            if (tokens.Count > MaxAreas)
            {
                problem = $"범위가 너무 많아요. {MaxAreas}개 이하로 나눠서 알려 주세요.";
                return false;
            }

            var parsed = new List<CellRange>(tokens.Count);
            foreach (var token in tokens)
            {
                if (!CellRange.TryParse(token, CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1, out var range))
                {
                    problem = $"알아볼 수 없는 범위예요: ‘{token}’. B2:D10이나 A1:C10 E1:E10처럼 알려 주세요.";
                    return false;
                }

                parsed.Add(range);
            }

            ranges = parsed;
            return true;
        }

        private static List<CellRange> Combine(IEnumerable<CellRange> first, IEnumerable<CellRange> second, Operation operation)
        {
            var a = ByTop(first);
            var b = ByTop(second);
            if (a.Count == 0 && (operation != Operation.Union || b.Count == 0))
                return new List<CellRange>();

            if (b.Count == 0 && operation == Operation.Intersect)
                return new List<CellRange>();

            var edges = new SortedSet<int>();
            foreach (var range in a.Concat(b))
            {
                edges.Add(range.Top);
                edges.Add(range.Bottom + 1);
            }

            var bands = edges.ToArray();
            var activeA = new List<CellRange>();
            var activeB = new List<CellRange>();
            var nextA = 0;
            var nextB = 0;
            var slabs = new List<Slab>();
            var previous = new List<(int Left, int Right, Slab Slab)>();
            for (var i = 0; i + 1 < bands.Length; i++)
            {
                var top = bands[i];
                var bottom = bands[i + 1] - 1;
                Activate(activeA, a, ref nextA, top);
                Activate(activeB, b, ref nextB, top);
                var intervals = Join(Merge(activeA.Select(r => (r.Left, r.Right))), Merge(activeB.Select(r => (r.Left, r.Right))), operation);
                var current = new List<(int Left, int Right, Slab Slab)>(intervals.Count);
                var cursor = 0;
                foreach (var (left, right) in intervals)
                {
                    while (cursor < previous.Count && previous[cursor].Left < left)
                        cursor++;

                    Slab slab;
                    if (cursor < previous.Count && previous[cursor].Left == left && previous[cursor].Right == right)
                    {
                        slab = previous[cursor].Slab;
                        slab.Bottom = bottom;
                    }
                    else
                    {
                        slab = new Slab(left, right, top, bottom);
                        slabs.Add(slab);
                    }

                    current.Add((left, right, slab));
                }

                previous = current;
            }

            return slabs.Select(s => new CellRange(s.Left, s.Top, s.Right, s.Bottom)).ToList();
        }

        private static List<CellRange> ByTop(IEnumerable<CellRange> ranges)
        {
            var list = (ranges ?? Enumerable.Empty<CellRange>()).Where(r => r != null).ToList();
            list.Sort((x, y) => x.Top.CompareTo(y.Top));
            return list;
        }

        private static void Activate(List<CellRange> active, List<CellRange> byTop, ref int next, int top)
        {
            active.RemoveAll(r => r.Bottom < top);
            while (next < byTop.Count && byTop[next].Top <= top)
            {
                var range = byTop[next++];
                var index = active.BinarySearch(range, s_byLeft);
                active.Insert(index < 0 ? ~index : index, range);
            }
        }

        private static List<(int Left, int Right)> Merge(IEnumerable<(int Left, int Right)> byLeft)
        {
            var merged = new List<(int Left, int Right)>();
            foreach (var (left, right) in byLeft)
            {
                var last = merged.Count - 1;
                if (last >= 0 && left <= merged[last].Right + 1)
                    merged[last] = (merged[last].Left, Math.Max(merged[last].Right, right));
                else
                    merged.Add((left, right));
            }

            return merged;
        }

        private static List<(int Left, int Right)> Join(List<(int Left, int Right)> x, List<(int Left, int Right)> y, Operation operation)
        {
            switch (operation)
            {
                case Operation.Intersect:
                    return Overlap(x, y);
                case Operation.Subtract:
                    return Remove(x, y);
                default:
                    return Merge(x.Concat(y).OrderBy(i => i.Left));
            }
        }

        private static List<(int Left, int Right)> Overlap(List<(int Left, int Right)> x, List<(int Left, int Right)> y)
        {
            var result = new List<(int Left, int Right)>();
            var i = 0;
            var j = 0;
            while (i < x.Count && j < y.Count)
            {
                var left = Math.Max(x[i].Left, y[j].Left);
                var right = Math.Min(x[i].Right, y[j].Right);
                if (left <= right)
                    result.Add((left, right));

                if (x[i].Right < y[j].Right)
                    i++;
                else
                    j++;
            }

            return result;
        }

        private static List<(int Left, int Right)> Remove(List<(int Left, int Right)> x, List<(int Left, int Right)> y)
        {
            var result = new List<(int Left, int Right)>();
            var first = 0;
            foreach (var (left, right) in x)
            {
                var start = left;
                while (first < y.Count && y[first].Right < start)
                    first++;

                for (var k = first; k < y.Count && y[k].Left <= right; k++)
                {
                    if (y[k].Left > start)
                        result.Add((start, y[k].Left - 1));

                    start = Math.Max(start, y[k].Right + 1);
                }

                if (start <= right)
                    result.Add((start, right));
            }

            return result;
        }

        private enum Operation
        {
            Union,
            Intersect,
            Subtract,
        }

        private sealed class Slab
        {
            public Slab(int left, int right, int top, int bottom)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }

            public int Left { get; }

            public int Right { get; }

            public int Top { get; }

            public int Bottom { get; set; }
        }
    }
}
