using System;
using System.Collections.Generic;
using System.Globalization;

namespace DocuHelper.Tools.Excel
{
    public sealed class CellRange
    {
        public CellRange(int left, int top, int right, int bottom)
        {
            Left = Math.Min(left, right);
            Right = Math.Max(left, right);
            Top = Math.Min(top, bottom);
            Bottom = Math.Max(top, bottom);
        }

        public int Left { get; }

        public int Top { get; }

        public int Right { get; }

        public int Bottom { get; }

        public int ColumnCount => Right - Left + 1;

        public int RowCount => Bottom - Top + 1;

        public long CellCount => (long)ColumnCount * RowCount;

        public bool IsSingleCell => Left == Right && Top == Bottom;

        public CellAddress Start => new(Left, Top);

        public CellAddress End => new(Right, Bottom);

        public static CellRange Of(CellAddress address) => new(address.Column, address.Row, address.Column, address.Row);

        public bool Contains(int column, int row) => column >= Left && column <= Right && row >= Top && row <= Bottom;

        public bool Contains(CellRange other) => other.Left >= Left && other.Right <= Right && other.Top >= Top && other.Bottom <= Bottom;

        public bool Intersects(CellRange other) => other.Left <= Right && other.Right >= Left && other.Top <= Bottom && other.Bottom >= Top;

        public CellRange Intersect(CellRange other)
        {
            if (other == null || !Intersects(other))
                return null;

            return new CellRange(Math.Max(Left, other.Left), Math.Max(Top, other.Top), Math.Min(Right, other.Right), Math.Min(Bottom, other.Bottom));
        }

        public CellRange Union(CellRange other)
        {
            if (other == null)
                return this;

            return new CellRange(Math.Min(Left, other.Left), Math.Min(Top, other.Top), Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom));
        }

        public IEnumerable<CellAddress> Cells()
        {
            for (var row = Top; row <= Bottom; row++)
            {
                for (var column = Left; column <= Right; column++)
                    yield return new CellAddress(column, row);
            }
        }

        public static bool TryParse(string text, out CellRange range) => TryParse(text, -1, -1, out range);

        public static bool TryParse(string text, int lastRow, int lastColumn, out CellRange range)
        {
            range = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var value = text.Trim().Replace(" ", string.Empty);
            var colon = value.IndexOf(':');
            if (colon < 0)
            {
                if (!CellAddress.TryParse(value, out var single))
                    return false;

                range = Of(single);
                return true;
            }

            var first = value.Substring(0, colon);
            var second = value.Substring(colon + 1);
            if (CellAddress.TryParse(first, out var start) && CellAddress.TryParse(second, out var end))
            {
                range = new CellRange(start.Column, start.Row, end.Column, end.Row);
                return true;
            }

            if (lastRow >= 0 && CellAddress.TryParseColumn(first.Trim('$'), out var left) && CellAddress.TryParseColumn(second.Trim('$'), out var right))
            {
                range = new CellRange(left, 0, right, lastRow);
                return true;
            }

            if (lastColumn >= 0 && CellAddress.TryParseRow(first.Trim('$'), out var top) && CellAddress.TryParseRow(second.Trim('$'), out var bottom))
            {
                range = new CellRange(0, top, lastColumn, bottom);
                return true;
            }

            return false;
        }

        public override string ToString() => IsSingleCell ? Start.ToString() : Start + ":" + End;

        public string RowsText() => RowCount == 1
            ? (Top + 1).ToString(CultureInfo.InvariantCulture)
            : (Top + 1).ToString(CultureInfo.InvariantCulture) + "-" + (Bottom + 1).ToString(CultureInfo.InvariantCulture);
    }
}
