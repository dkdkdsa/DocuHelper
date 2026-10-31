using System;
using System.Collections.Generic;
using System.Text;

namespace DocuHelper.Tools.Word
{
    public static class WordTextDiff
    {
        private const int MaxCells = 4_000_000;
        private const int MaxDistance = 600;

        public static IReadOnlyList<WordTextHunk> Compute(string before, string after)
        {
            var a = before ?? string.Empty;
            var b = after ?? string.Empty;
            var hunks = new List<WordTextHunk>();
            if (string.Equals(a, b, StringComparison.Ordinal))
                return hunks;

            var prefix = 0;
            while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix])
                prefix++;

            if (prefix > 0 && char.IsHighSurrogate(a[prefix - 1]))
                prefix--;

            var suffix = 0;
            while (suffix < a.Length - prefix && suffix < b.Length - prefix && a[a.Length - 1 - suffix] == b[b.Length - 1 - suffix])
                suffix++;

            if (suffix > 0 && char.IsLowSurrogate(a[a.Length - suffix]))
                suffix--;

            var middleA = a.Substring(prefix, a.Length - prefix - suffix);
            var middleB = b.Substring(prefix, b.Length - prefix - suffix);
            if (middleA.Length == 0 || middleB.Length == 0 || (long)middleA.Length * middleB.Length > MaxCells)
            {
                hunks.Add(new WordTextHunk(prefix, prefix + middleA.Length, middleB));
                return hunks;
            }

            var pointsA = Points(middleA, out var offsetsA);
            var pointsB = Points(middleB, out var offsetsB);
            var trace = Trace(pointsA, pointsB);
            if (trace == null)
            {
                hunks.Add(new WordTextHunk(prefix, prefix + middleA.Length, middleB));
                return hunks;
            }

            var moves = Backtrack(trace, pointsA.Length, pointsB.Length);
            var start = -1;
            var end = 0;
            var insert = new StringBuilder();
            foreach (var (fromX, fromY, toX, toY) in moves)
            {
                if (toX > fromX && toY > fromY)
                {
                    Flush(hunks, ref start, end, insert, prefix);
                    continue;
                }

                if (start < 0)
                {
                    start = offsetsA[fromX];
                    end = start;
                }

                if (toX > fromX)
                {
                    end = offsetsA[toX];
                }
                else
                {
                    end = Math.Max(end, offsetsA[fromX]);
                    insert.Append(middleB, offsetsB[fromY], offsetsB[toY] - offsetsB[fromY]);
                }
            }

            Flush(hunks, ref start, end, insert, prefix);
            return hunks;
        }

        private static void Flush(List<WordTextHunk> hunks, ref int start, int end, StringBuilder insert, int prefix)
        {
            if (start < 0)
                return;

            hunks.Add(new WordTextHunk(prefix + start, prefix + Math.Max(start, end), insert.ToString()));
            start = -1;
            insert.Clear();
        }

        private static int[] Points(string text, out int[] offsets)
        {
            var points = new List<int>();
            var starts = new List<int>();
            for (var i = 0; i < text.Length; i++)
            {
                starts.Add(i);
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    points.Add(char.ConvertToUtf32(text[i], text[i + 1]));
                    i++;
                }
                else
                {
                    points.Add(text[i]);
                }
            }

            starts.Add(text.Length);
            offsets = starts.ToArray();
            return points.ToArray();
        }

        private static List<int[]> Trace(int[] a, int[] b)
        {
            var n = a.Length;
            var m = b.Length;
            var max = Math.Min(n + m, MaxDistance);
            var offset = max + 1;
            var v = new int[2 * max + 3];
            var trace = new List<int[]>();
            for (var d = 0; d <= max; d++)
            {
                var snapshot = new int[2 * d + 3];
                Array.Copy(v, offset - d - 1, snapshot, 0, 2 * d + 3);
                trace.Add(snapshot);
                for (var k = -d; k <= d; k += 2)
                {
                    int x;
                    if (k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]))
                        x = v[offset + k + 1];
                    else
                        x = v[offset + k - 1] + 1;

                    var y = x - k;
                    while (x < n && y < m && a[x] == b[y])
                    {
                        x++;
                        y++;
                    }

                    v[offset + k] = x;
                    if (x >= n && y >= m)
                        return trace;
                }
            }

            return null;
        }

        private static List<(int FromX, int FromY, int ToX, int ToY)> Backtrack(List<int[]> trace, int n, int m)
        {
            var moves = new List<(int, int, int, int)>();
            var x = n;
            var y = m;
            for (var d = trace.Count - 1; d >= 0; d--)
            {
                var snapshot = trace[d];
                var k = x - y;
                int previousK;
                if (k == -d || (k != d && snapshot[k - 1 + d + 1] < snapshot[k + 1 + d + 1]))
                    previousK = k + 1;
                else
                    previousK = k - 1;

                var previousX = snapshot[previousK + d + 1];
                var previousY = previousX - previousK;
                while (x > previousX && y > previousY)
                {
                    moves.Add((x - 1, y - 1, x, y));
                    x--;
                    y--;
                }

                if (d > 0)
                    moves.Add((previousX, previousY, x, y));

                x = previousX;
                y = previousY;
            }

            moves.Reverse();
            return moves;
        }
    }
}
