using System.Collections.Generic;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed class TableLook
    {
        private const int Whole = 0;
        private const int ColumnStripe = 1;
        private const int RowStripe = 2;
        private const int LastColumn = 3;
        private const int FirstColumn = 4;
        private const int HeaderRow = 5;
        private const int TotalRow = 6;

        private readonly TableStyleDefinition _style;
        private readonly ExcelColors _colors;
        private readonly Dictionary<(int Theme, double Tint), string> _resolved = new();
        private readonly int _rows;
        private readonly int _columns;
        private readonly int _dataTop;
        private readonly int _dataBottom;
        private readonly bool _header;
        private readonly bool _totals;
        private readonly bool _firstColumn;
        private readonly bool _lastColumn;
        private readonly bool _bandedRows;
        private readonly bool _bandedColumns;

        private TableLook(ExcelBook book, ExcelTable table)
        {
            _style = TableStyleCatalog.Find(table.StyleName);
            _colors = new ExcelColors(book.WorkbookPart);
            _rows = table.Range.RowCount;
            _columns = table.Range.ColumnCount;
            _header = table.HasHeader;
            _totals = table.HasTotals;
            _dataTop = _header ? 1 : 0;
            _dataBottom = _totals ? _rows - 2 : _rows - 1;
            _firstColumn = table.FirstColumn;
            _lastColumn = table.LastColumn;
            _bandedRows = table.BandedRows;
            _bandedColumns = table.BandedColumns;
        }

        public bool IsEmpty => _style == null;

        public static TableLook Of(ExcelBook book, ExcelTable table) => new(book, table);

        public StyleChange At(int row, int column)
        {
            if (_style == null || row < 0 || row >= _rows || column < 0 || column >= _columns)
                return null;

            var parts = Applicable(row, column);
            var change = new StyleChange();
            ThemeShade? fill = null;
            ThemeShade? ink = null;
            for (var i = parts.Count - 1; i >= 0; i--)
            {
                var element = parts[i].Element;
                fill ??= element.Fill;
                ink ??= element.FontColor;
            }

            if (fill.HasValue)
                change.FillColor = Resolve(fill.Value);

            if (ink.HasValue && !(ink.Value.Theme == ThemeShade.Dark1 && ink.Value.Tint == 0))
                change.FontColor = Resolve(ink.Value);

            if (parts.Exists(p => p.Element.Bold))
                change.Bold = true;

            change.Top = Edge(row, column, true, true);
            change.Bottom = Edge(row, column, true, false);
            change.Left = Edge(row, column, false, true);
            change.Right = Edge(row, column, false, false);
            return change.IsEmpty ? null : change;
        }

        private ExcelBorder Edge(int row, int column, bool horizontalEdge, bool leading)
        {
            var own = Claim(row, column, horizontalEdge, leading);
            var nextRow = horizontalEdge ? (leading ? row - 1 : row + 1) : row;
            var nextColumn = horizontalEdge ? column : (leading ? column - 1 : column + 1);
            var other = nextRow >= 0 && nextRow < _rows && nextColumn >= 0 && nextColumn < _columns
                ? Claim(nextRow, nextColumn, horizontalEdge, !leading)
                : (Rank: -1, Line: (TableStyleLine)null);

            var winner = other.Rank > own.Rank ? other : own;
            return winner.Line == null ? null : new ExcelBorder(winner.Line.Style, Resolve(winner.Line.Color));
        }

        private (int Rank, TableStyleLine Line) Claim(int row, int column, bool horizontalEdge, bool leading)
        {
            var best = (Rank: -1, Line: (TableStyleLine)null);
            foreach (var part in Applicable(row, column))
            {
                TableStyleLine line;
                if (horizontalEdge)
                {
                    var atEdge = leading ? row == part.Top : row == part.Bottom;
                    line = atEdge ? (leading ? part.Element.Top : part.Element.Bottom) : part.Element.Horizontal;
                }
                else
                {
                    var atEdge = leading ? column == part.Left : column == part.Right;
                    line = atEdge ? (leading ? part.Element.Left : part.Element.Right) : part.Element.Vertical;
                }

                if (line != null && part.Rank >= best.Rank)
                    best = (part.Rank, line);
            }

            return best;
        }

        private List<Part> Applicable(int row, int column)
        {
            var parts = new List<Part> { new(Whole, _style.WholeTable, 0, _rows - 1, 0, _columns - 1) };
            var isData = row >= _dataTop && row <= _dataBottom;
            if (_bandedColumns && isData && column % 2 == 0 && _style.ColumnStripe != null)
                parts.Add(new Part(ColumnStripe, _style.ColumnStripe, _dataTop, _dataBottom, column, column));

            if (_bandedRows && isData && (row - _dataTop) % 2 == 0 && _style.RowStripe != null)
                parts.Add(new Part(RowStripe, _style.RowStripe, row, row, 0, _columns - 1));

            if (_lastColumn && column == _columns - 1 && _style.LastColumn != null)
                parts.Add(new Part(LastColumn, _style.LastColumn, 0, _rows - 1, column, column));

            if (_firstColumn && column == 0 && _style.FirstColumn != null)
                parts.Add(new Part(FirstColumn, _style.FirstColumn, 0, _rows - 1, 0, 0));

            if (_header && row == 0 && _style.HeaderRow != null)
                parts.Add(new Part(HeaderRow, _style.HeaderRow, 0, 0, 0, _columns - 1));

            if (_totals && row == _rows - 1 && _style.TotalRow != null)
                parts.Add(new Part(TotalRow, _style.TotalRow, row, row, 0, _columns - 1));

            return parts;
        }

        private string Resolve(ThemeShade shade)
        {
            var key = (shade.Theme, shade.Tint);
            if (!_resolved.TryGetValue(key, out var hex))
            {
                hex = _colors.Resolve(new Color { Theme = (uint)shade.Theme, Tint = shade.Tint });
                _resolved[key] = hex;
            }

            return hex;
        }

        private readonly struct Part
        {
            public Part(int rank, TableStyleElement element, int top, int bottom, int left, int right)
            {
                Rank = rank;
                Element = element;
                Top = top;
                Bottom = bottom;
                Left = left;
                Right = right;
            }

            public int Rank { get; }

            public TableStyleElement Element { get; }

            public int Top { get; }

            public int Bottom { get; }

            public int Left { get; }

            public int Right { get; }
        }
    }
}
