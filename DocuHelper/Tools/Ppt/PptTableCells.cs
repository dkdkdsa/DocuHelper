using System;
using System.Collections.Generic;
using System.Linq;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocuHelper.Tools.Ppt
{
    public sealed class PptTableCells
    {
        private readonly List<A.TableRow> _rowElements;
        private readonly List<List<A.TableCell>> _rows;
        private readonly List<A.GridColumn> _columnElements;

        private PptTableCells(A.Table table, List<A.TableRow> rowElements, List<List<A.TableCell>> rows, List<A.GridColumn> columnElements)
        {
            Table = table;
            _rowElements = rowElements;
            _rows = rows;
            _columnElements = columnElements;
        }

        public A.Table Table { get; }

        public List<PptTableRegion> Regions { get; } = new();

        public int RowCount => _rows.Count;

        public int ColumnCount => _columnElements.Count;

        public long Width => _columnElements.Sum(c => c.Width?.Value ?? 0);

        public long Height => _rowElements.Sum(r => r.Height?.Value ?? 0);

        public static PptTableCells Load(A.Table table)
        {
            var columns = table.TableGrid?.Elements<A.GridColumn>().ToList() ?? new List<A.GridColumn>();
            var rowElements = table.Elements<A.TableRow>().ToList();
            var rows = rowElements.Select(r => r.Elements<A.TableCell>().ToList()).ToList();
            if (columns.Count == 0 || rows.Count == 0 || rows.Any(r => r.Count != columns.Count))
                throw new ToolInputException("이 표는 칸 구조가 맞지 않아서 고칠 수 없어요. 표를 새로 만들어 주세요.");

            var cells = new PptTableCells(table, rowElements, rows, columns);
            for (var r = 0; r < rows.Count; r++)
            {
                for (var c = 0; c < columns.Count; c++)
                {
                    var cell = rows[r][c];
                    if (cell.HorizontalMerge?.Value == true || cell.VerticalMerge?.Value == true)
                        continue;

                    var columnSpan = Math.Clamp(cell.GridSpan?.Value ?? 1, 1, columns.Count - c);
                    var rowSpan = Math.Clamp(cell.RowSpan?.Value ?? 1, 1, rows.Count - r);
                    if (columnSpan == 1 && rowSpan == 1)
                        continue;

                    if (cells.Regions.Any(x => x.Intersects(r, c, rowSpan, columnSpan)))
                        continue;

                    cells.Regions.Add(new PptTableRegion(r, c, rowSpan, columnSpan));
                }
            }

            return cells;
        }

        public A.TableCell Cell(int row, int column) => _rows[row][column];

        public A.TableRow RowElement(int row) => _rowElements[row];

        public A.GridColumn ColumnElement(int column) => _columnElements[column];

        public PptTableRegion RegionAt(int row, int column) => Regions.FirstOrDefault(r => r.Contains(row, column));

        public bool IsCovered(int row, int column)
        {
            var region = RegionAt(row, column);
            return region != null && (region.Row != row || region.Column != column);
        }

        public static string TextOf(A.TableCell cell) =>
            string.Join("\n", cell.TextBody?.Elements<A.Paragraph>().Select(PptText.TextOf) ?? Enumerable.Empty<string>());

        public static bool IsBlank(A.TableCell cell) => TextOf(cell).Length == 0;

        public void SetText(int row, int column, string text) => SetText(Cell(row, column), text);

        public static void SetText(A.TableCell cell, string text)
        {
            var body = BodyOf(cell);
            var template = body.Elements<A.Paragraph>().FirstOrDefault();
            var paragraph = PptText.Build(template, text, 0);
            body.RemoveAllChildren<A.Paragraph>();
            body.AppendChild(paragraph);
        }

        public void InsertRows(int at, int count)
        {
            var reference = Math.Max(0, at - 1);
            for (var i = 0; i < count; i++)
            {
                var clone = (A.TableRow)_rowElements[reference].CloneNode(true);
                clone.RemoveAllChildren<A.ExtensionList>();
                var blanks = new List<A.TableCell>();
                foreach (var cell in clone.Elements<A.TableCell>().ToList())
                {
                    var blank = Blank(cell);
                    cell.InsertBeforeSelf(blank);
                    cell.Remove();
                    blanks.Add(blank);
                }

                if (at < _rowElements.Count)
                    _rowElements[at].InsertBeforeSelf(clone);
                else
                    _rowElements[^1].InsertAfterSelf(clone);

                _rowElements.Insert(at, clone);
                _rows.Insert(at, blanks);
            }

            foreach (var region in Regions)
            {
                if (region.Row >= at)
                    region.Row += count;
                else if (region.Row + region.RowSpan > at)
                    region.RowSpan += count;
            }
        }

        public void DeleteRows(int at, int count)
        {
            for (var i = 0; i < count; i++)
            {
                _rowElements[at].Remove();
                _rowElements.RemoveAt(at);
                _rows.RemoveAt(at);
            }

            foreach (var region in Regions.ToList())
            {
                var overlap = Math.Max(0, Math.Min(region.Row + region.RowSpan, at + count) - Math.Max(region.Row, at));
                if (overlap == 0)
                {
                    if (region.Row >= at + count)
                        region.Row -= count;

                    continue;
                }

                region.RowSpan -= overlap;
                if (region.Row >= at)
                    region.Row = at;

                if (region.RowSpan <= 0 || (region.RowSpan == 1 && region.ColumnSpan == 1))
                    Regions.Remove(region);
            }
        }

        public void InsertColumns(int at, int count)
        {
            var reference = Math.Max(0, at - 1);
            var width = _columnElements[reference].Width?.Value ?? 914400;
            for (var i = 0; i < count; i++)
            {
                var column = new A.GridColumn { Width = width };
                if (at < _columnElements.Count)
                    _columnElements[at].InsertBeforeSelf(column);
                else
                    _columnElements[^1].InsertAfterSelf(column);

                _columnElements.Insert(at, column);
                for (var r = 0; r < _rows.Count; r++)
                {
                    var blank = Blank(_rows[r][reference]);
                    if (at < _rows[r].Count)
                        _rows[r][at].InsertBeforeSelf(blank);
                    else
                        _rows[r][^1].InsertAfterSelf(blank);

                    _rows[r].Insert(at, blank);
                }
            }

            foreach (var region in Regions)
            {
                if (region.Column >= at)
                    region.Column += count;
                else if (region.Column + region.ColumnSpan > at)
                    region.ColumnSpan += count;
            }
        }

        public void DeleteColumns(int at, int count)
        {
            for (var i = 0; i < count; i++)
            {
                _columnElements[at].Remove();
                _columnElements.RemoveAt(at);
                foreach (var row in _rows)
                {
                    row[at].Remove();
                    row.RemoveAt(at);
                }
            }

            foreach (var region in Regions.ToList())
            {
                var overlap = Math.Max(0, Math.Min(region.Column + region.ColumnSpan, at + count) - Math.Max(region.Column, at));
                if (overlap == 0)
                {
                    if (region.Column >= at + count)
                        region.Column -= count;

                    continue;
                }

                region.ColumnSpan -= overlap;
                if (region.Column >= at)
                    region.Column = at;

                if (region.ColumnSpan <= 0 || (region.RowSpan == 1 && region.ColumnSpan == 1))
                    Regions.Remove(region);
            }
        }

        public void Merge(int row, int column, int rowSpan, int columnSpan)
        {
            var origin = Cell(row, column);
            var body = BodyOf(origin);
            var kept = IsBlank(origin) ? new List<A.Paragraph>() : body.Elements<A.Paragraph>().ToList();
            var moved = new List<A.Paragraph>();
            for (var r = row; r < row + rowSpan; r++)
            {
                for (var c = column; c < column + columnSpan; c++)
                {
                    if (r == row && c == column)
                        continue;

                    var cell = Cell(r, c);
                    if (IsBlank(cell))
                        continue;

                    moved.AddRange(cell.TextBody.Elements<A.Paragraph>().Where(p => PptText.TextOf(p).Length > 0).Select(p => (A.Paragraph)p.CloneNode(true)));
                    SetText(cell, string.Empty);
                }
            }

            if (moved.Count > 0)
            {
                if (kept.Count == 0)
                    body.RemoveAllChildren<A.Paragraph>();

                foreach (var paragraph in moved)
                    body.AppendChild(paragraph);
            }

            Regions.Add(new PptTableRegion(row, column, rowSpan, columnSpan));
        }

        public PptTableRegion Unmerge(int row, int column)
        {
            var region = RegionAt(row, column);
            if (region != null)
                Regions.Remove(region);

            return region;
        }

        public void Commit()
        {
            foreach (var row in _rows)
            {
                foreach (var cell in row)
                {
                    cell.GridSpan = null;
                    cell.RowSpan = null;
                    cell.HorizontalMerge = null;
                    cell.VerticalMerge = null;
                }
            }

            foreach (var region in Regions)
            {
                for (var r = region.Row; r < region.Row + region.RowSpan; r++)
                {
                    for (var c = region.Column; c < region.Column + region.ColumnSpan; c++)
                    {
                        var cell = _rows[r][c];
                        if (r == region.Row && c == region.Column)
                        {
                            if (region.ColumnSpan > 1)
                                cell.GridSpan = region.ColumnSpan;

                            if (region.RowSpan > 1)
                                cell.RowSpan = region.RowSpan;

                            continue;
                        }

                        if (c > region.Column)
                            cell.HorizontalMerge = true;

                        if (r > region.Row)
                            cell.VerticalMerge = true;
                    }
                }
            }
        }

        private static A.TextBody BodyOf(A.TableCell cell)
        {
            if (cell.TextBody != null)
                return cell.TextBody;

            var body = new A.TextBody(new A.BodyProperties(), new A.ListStyle());
            cell.PrependChild(body);
            return body;
        }

        private static A.TableCell Blank(A.TableCell source)
        {
            var clone = (A.TableCell)source.CloneNode(true);
            clone.GridSpan = null;
            clone.RowSpan = null;
            clone.HorizontalMerge = null;
            clone.VerticalMerge = null;
            SetText(clone, string.Empty);
            return clone;
        }
    }
}
