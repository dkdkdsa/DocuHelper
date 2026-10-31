using System;

namespace DocuHelper.Tools.Word
{
    public readonly record struct WordTableRange(int Row1, int Column1, int Row2, int Column2)
    {
        public int Rows => Row2 - Row1 + 1;

        public int Columns => Column2 - Column1 + 1;

        public bool IsSingle => Rows == 1 && Columns == 1;

        public bool Contains(int row, int column) => row >= Row1 && row <= Row2 && column >= Column1 && column <= Column2;

        public bool Covers(WordTableRange other) =>
            other.Row1 >= Row1 && other.Row2 <= Row2 && other.Column1 >= Column1 && other.Column2 <= Column2;

        public WordTableRange Union(WordTableRange other) => new(
            Math.Min(Row1, other.Row1),
            Math.Min(Column1, other.Column1),
            Math.Max(Row2, other.Row2),
            Math.Max(Column2, other.Column2));
    }
}
