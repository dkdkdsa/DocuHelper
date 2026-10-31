using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    public sealed class CellShift
    {
        private readonly LineShift _axis;

        public CellShift(CellRange band, bool insert, bool vertical)
        {
            Band = band;
            IsInsert = insert;
            Vertical = vertical;
            var size = vertical ? band.RowCount : band.ColumnCount;
            _axis = new LineShift(vertical, vertical ? band.Top : band.Left, insert ? size : -size);
            Affected = vertical
                ? new CellRange(band.Left, band.Top, band.Right, CellAddress.MaxRows - 1)
                : new CellRange(band.Left, band.Top, CellAddress.MaxColumns - 1, band.Bottom);
        }

        public CellRange Band { get; }

        public CellRange Affected { get; }

        public bool IsInsert { get; }

        public bool Vertical { get; }

        public int Size => _axis.Size;

        public int Delta => _axis.Count;

        public string Direction => Vertical ? (IsInsert ? "down" : "up") : (IsInsert ? "right" : "left");

        public bool Aligned(CellRange range) => Vertical
            ? range.Left >= Band.Left && range.Right <= Band.Right
            : range.Top >= Band.Top && range.Bottom <= Band.Bottom;

        public CellAddress? Map(CellAddress address)
        {
            if (!Affected.Contains(address.Column, address.Row))
                return address;

            var index = _axis.Index(Vertical ? address.Row : address.Column);
            if (!index.HasValue)
                return null;

            return Vertical ? new CellAddress(address.Column, index.Value) : new CellAddress(index.Value, address.Row);
        }

        public IReadOnlyList<CellRange> Pieces(CellRange range)
        {
            if (!range.Intersects(Affected))
                return new[] { range };

            if (Aligned(range))
            {
                var mapped = _axis.Map(range);
                return mapped == null ? Array.Empty<CellRange>() : new[] { mapped };
            }

            var pieces = new List<CellRange>(SqrefMath.Subtract(range, Affected));
            var inside = range.Intersect(Affected);
            IEnumerable<CellRange> moving = IsInsert ? new[] { inside } : SqrefMath.Subtract(inside, Band);
            pieces.AddRange(SqrefMath.Translate(moving, Vertical ? 0 : Delta, Vertical ? Delta : 0));
            return pieces;
        }

        public string MapSqref(string sqref)
        {
            if (!SqrefMath.TryParse(sqref, out var ranges))
                return sqref;

            if (!ranges.Any(r => r.Intersects(Affected)))
                return sqref;

            var pieces = ranges.SelectMany(Pieces).ToList();
            return pieces.Count == 0 ? null : SqrefMath.Format(SqrefMath.Normalize(pieces));
        }

        public FormulaReference Map(FormulaReference reference, out bool resized)
        {
            resized = false;
            if (reference.Kind == FormulaReferenceKind.Columns || reference.Kind == FormulaReferenceKind.Rows)
                return reference.Clone();

            var first = Math.Min(Vertical ? reference.Column1 : reference.Row1, Vertical ? reference.Column2 : reference.Row2);
            var last = Math.Max(Vertical ? reference.Column1 : reference.Row1, Vertical ? reference.Column2 : reference.Row2);
            var from = Vertical ? Band.Left : Band.Top;
            var to = Vertical ? Band.Right : Band.Bottom;
            if (first < from || last > to)
                return reference.Clone();

            return _axis.Map(reference, out resized);
        }
    }
}
