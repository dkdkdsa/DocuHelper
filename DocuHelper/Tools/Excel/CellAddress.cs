using System;
using System.Globalization;

namespace DocuHelper.Tools.Excel
{
    public readonly struct CellAddress : IEquatable<CellAddress>
    {
        public const int MaxColumns = 16384;

        public const int MaxRows = 1048576;

        public CellAddress(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public int Column { get; }

        public int Row { get; }

        public bool IsValid => Column >= 0 && Column < MaxColumns && Row >= 0 && Row < MaxRows;

        public static string ColumnName(int index)
        {
            var name = string.Empty;
            var number = index + 1;
            while (number > 0)
            {
                var remainder = (number - 1) % 26;
                name = (char)('A' + remainder) + name;
                number = (number - 1) / 26;
            }

            return name;
        }

        public static bool TryParseColumn(string text, out int column)
        {
            column = -1;
            if (string.IsNullOrEmpty(text) || text.Length > 3)
                return false;

            var value = 0;
            foreach (var c in text)
            {
                var upper = char.ToUpperInvariant(c);
                if (upper < 'A' || upper > 'Z')
                    return false;

                value = (value * 26) + (upper - 'A' + 1);
            }

            if (value > MaxColumns)
                return false;

            column = value - 1;
            return true;
        }

        public static bool TryParseRow(string text, out int row)
        {
            row = -1;
            if (string.IsNullOrEmpty(text) || text.Length > 7)
                return false;

            foreach (var c in text)
            {
                if (!char.IsAsciiDigit(c))
                    return false;
            }

            var number = int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
            if (number < 1 || number > MaxRows)
                return false;

            row = number - 1;
            return true;
        }

        public static bool TryParse(string text, out CellAddress address)
        {
            address = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var value = text.Trim();
            var index = 0;
            if (value[index] == '$')
                index++;

            var letterStart = index;
            while (index < value.Length && char.IsAsciiLetter(value[index]))
                index++;

            if (!TryParseColumn(value.Substring(letterStart, index - letterStart), out var column))
                return false;

            if (index < value.Length && value[index] == '$')
                index++;

            if (!TryParseRow(value.Substring(index), out var row))
                return false;

            address = new CellAddress(column, row);
            return true;
        }

        public bool Equals(CellAddress other) => Column == other.Column && Row == other.Row;

        public override bool Equals(object obj) => obj is CellAddress other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Column, Row);

        public override string ToString() => ColumnName(Column) + (Row + 1).ToString(CultureInfo.InvariantCulture);
    }
}
