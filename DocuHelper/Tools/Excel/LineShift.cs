using System;

namespace DocuHelper.Tools.Excel
{
    public sealed class LineShift
    {
        public LineShift(bool rows, int at, int count)
        {
            Rows = rows;
            At = at;
            Count = count;
        }

        public bool Rows { get; }

        public int At { get; }

        public int Count { get; }

        public int Size => Math.Abs(Count);

        public bool IsInsert => Count > 0;

        public int End => At + Size - 1;

        public int Limit => Rows ? CellAddress.MaxRows : CellAddress.MaxColumns;

        public int? Index(int index)
        {
            if (index < At)
                return index;

            if (IsInsert)
                return index + Count < Limit ? index + Count : null;

            return index <= End ? null : index - Size;
        }

        public (int First, int Last)? Span(int first, int last)
        {
            if (first > last)
                (first, last) = (last, first);

            if (last < At)
                return (first, last);

            if (IsInsert)
            {
                if (first >= At)
                {
                    if (first + Count >= Limit)
                        return null;

                    return (first + Count, Math.Min(last + Count, Limit - 1));
                }

                return (first, Math.Min(last + Count, Limit - 1));
            }

            if (first > End)
                return (first - Size, last - Size);

            if (first >= At && last <= End)
                return null;

            return (first < At ? first : At, last > End ? last - Size : At - 1);
        }

        public CellAddress? Map(CellAddress address)
        {
            if (Rows)
            {
                var row = Index(address.Row);
                return row.HasValue ? new CellAddress(address.Column, row.Value) : null;
            }

            var column = Index(address.Column);
            return column.HasValue ? new CellAddress(column.Value, address.Row) : null;
        }

        public CellRange Map(CellRange range)
        {
            if (Rows)
            {
                var span = Span(range.Top, range.Bottom);
                return span.HasValue ? new CellRange(range.Left, span.Value.First, range.Right, span.Value.Last) : null;
            }

            var columns = Span(range.Left, range.Right);
            return columns.HasValue ? new CellRange(columns.Value.First, range.Top, columns.Value.Last, range.Bottom) : null;
        }

        public bool Resizes(CellRange range)
        {
            var mapped = Map(range);
            return mapped == null || mapped.RowCount != range.RowCount || mapped.ColumnCount != range.ColumnCount;
        }

        public FormulaReference Map(FormulaReference reference, out bool resized)
        {
            resized = false;
            var copy = reference.Clone();
            switch (reference.Kind)
            {
                case FormulaReferenceKind.Cell:
                    var index = Index(Rows ? reference.Row1 : reference.Column1);
                    if (!index.HasValue)
                    {
                        resized = true;
                        return null;
                    }

                    if (Rows)
                    {
                        copy.Row1 = index.Value;
                        copy.Row2 = index.Value;
                    }
                    else
                    {
                        copy.Column1 = index.Value;
                        copy.Column2 = index.Value;
                    }

                    return copy;
                case FormulaReferenceKind.Columns when Rows:
                case FormulaReferenceKind.Rows when !Rows:
                    return copy;
            }

            var first = Rows ? reference.Row1 : reference.Column1;
            var last = Rows ? reference.Row2 : reference.Column2;
            var span = Span(first, last);
            if (!span.HasValue)
            {
                resized = true;
                return null;
            }

            var (newFirst, newLast) = first <= last ? span.Value : (span.Value.Last, span.Value.First);
            resized = Math.Abs(newLast - newFirst) != Math.Abs(last - first);
            if (Rows)
            {
                copy.Row1 = newFirst;
                copy.Row2 = newLast;
            }
            else
            {
                copy.Column1 = newFirst;
                copy.Column2 = newLast;
            }

            return copy;
        }
    }
}
