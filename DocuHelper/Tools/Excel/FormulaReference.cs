using System;
using System.Globalization;
using System.Text;

namespace DocuHelper.Tools.Excel
{
    public sealed class FormulaReference
    {
        public FormulaReferenceKind Kind { get; set; }

        public string Sheet { get; set; }

        public string SheetPrefix { get; set; }

        public int Column1 { get; set; }

        public int Row1 { get; set; }

        public int Column2 { get; set; }

        public int Row2 { get; set; }

        public bool Column1Absolute { get; set; }

        public bool Row1Absolute { get; set; }

        public bool Column2Absolute { get; set; }

        public bool Row2Absolute { get; set; }

        public bool IsInBounds =>
            Column1 >= 0 && Column2 >= 0 && Column1 < CellAddress.MaxColumns && Column2 < CellAddress.MaxColumns
            && Row1 >= 0 && Row2 >= 0 && Row1 < CellAddress.MaxRows && Row2 < CellAddress.MaxRows;

        public bool IsOnSheet(string sheetName, string currentSheet) =>
            string.Equals(Sheet ?? currentSheet, sheetName, StringComparison.OrdinalIgnoreCase);

        public FormulaReference Clone() => (FormulaReference)MemberwiseClone();

        public CellRange ToRange(int lastRow, int lastColumn) => Kind switch
        {
            FormulaReferenceKind.Cell => new CellRange(Column1, Row1, Column1, Row1),
            FormulaReferenceKind.Area => new CellRange(Column1, Row1, Column2, Row2),
            FormulaReferenceKind.Columns => new CellRange(Column1, 0, Column2, Math.Max(0, lastRow)),
            _ => new CellRange(0, Row1, Math.Max(0, lastColumn), Row2),
        };

        public CellRange ToFullRange() => ToRange(CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1);

        public FormulaReference Shift(int rowDelta, int columnDelta)
        {
            var copy = Clone();
            if (Kind != FormulaReferenceKind.Rows)
            {
                if (!Column1Absolute)
                    copy.Column1 += columnDelta;

                if (!Column2Absolute)
                    copy.Column2 += columnDelta;
            }

            if (Kind != FormulaReferenceKind.Columns)
            {
                if (!Row1Absolute)
                    copy.Row1 += rowDelta;

                if (!Row2Absolute)
                    copy.Row2 += rowDelta;
            }

            if (Kind == FormulaReferenceKind.Cell)
            {
                copy.Column2 = copy.Column1;
                copy.Row2 = copy.Row1;
            }

            return copy.IsInBounds ? copy : null;
        }

        public FormulaReference MoveRows(int rowDelta)
        {
            var copy = Clone();
            if (Kind == FormulaReferenceKind.Columns)
                return copy;

            copy.Row1 += rowDelta;
            copy.Row2 += rowDelta;
            return copy.IsInBounds ? copy : null;
        }

        public string Render()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(SheetPrefix))
                builder.Append(SheetPrefix);
            else if (!string.IsNullOrEmpty(Sheet))
                builder.Append(QuoteSheet(Sheet)).Append('!');

            switch (Kind)
            {
                case FormulaReferenceKind.Cell:
                    AppendCell(builder, Column1, Column1Absolute, Row1, Row1Absolute);
                    break;
                case FormulaReferenceKind.Area:
                    AppendCell(builder, Column1, Column1Absolute, Row1, Row1Absolute);
                    builder.Append(':');
                    AppendCell(builder, Column2, Column2Absolute, Row2, Row2Absolute);
                    break;
                case FormulaReferenceKind.Columns:
                    AppendColumn(builder, Column1, Column1Absolute);
                    builder.Append(':');
                    AppendColumn(builder, Column2, Column2Absolute);
                    break;
                default:
                    AppendRow(builder, Row1, Row1Absolute);
                    builder.Append(':');
                    AppendRow(builder, Row2, Row2Absolute);
                    break;
            }

            return builder.ToString();
        }

        public string RenderPrefix()
        {
            if (!string.IsNullOrEmpty(SheetPrefix))
                return SheetPrefix;

            return string.IsNullOrEmpty(Sheet) ? string.Empty : QuoteSheet(Sheet) + "!";
        }

        public static string QuoteSheet(string name)
        {
            var plain = name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_');
            foreach (var c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                {
                    plain = false;
                    break;
                }
            }

            if (plain && (CellAddress.TryParse(name, out _) || CellAddress.TryParseColumn(name, out _)))
                plain = false;

            return plain ? name : "'" + name.Replace("'", "''") + "'";
        }

        public override string ToString() => Render();

        private static void AppendCell(StringBuilder builder, int column, bool columnAbsolute, int row, bool rowAbsolute)
        {
            AppendColumn(builder, column, columnAbsolute);
            AppendRow(builder, row, rowAbsolute);
        }

        private static void AppendColumn(StringBuilder builder, int column, bool absolute)
        {
            if (absolute)
                builder.Append('$');

            builder.Append(CellAddress.ColumnName(column));
        }

        private static void AppendRow(StringBuilder builder, int row, bool absolute)
        {
            if (absolute)
                builder.Append('$');

            builder.Append((row + 1).ToString(CultureInfo.InvariantCulture));
        }
    }
}
