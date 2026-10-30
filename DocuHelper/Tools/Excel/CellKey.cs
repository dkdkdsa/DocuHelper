using System;

namespace DocuHelper.Tools.Excel
{
    public readonly struct CellKey : IEquatable<CellKey>
    {
        public CellKey(int sheetIndex, int column, int row)
        {
            SheetIndex = sheetIndex;
            Column = column;
            Row = row;
        }

        public int SheetIndex { get; }

        public int Column { get; }

        public int Row { get; }

        public CellAddress Address => new(Column, Row);

        public bool Equals(CellKey other) => SheetIndex == other.SheetIndex && Column == other.Column && Row == other.Row;

        public override bool Equals(object obj) => obj is CellKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(SheetIndex, Column, Row);
    }
}
