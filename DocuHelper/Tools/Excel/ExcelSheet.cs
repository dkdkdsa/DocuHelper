using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public sealed class ExcelSheet
    {
        private Dictionary<long, Cell> _cells;
        private Dictionary<int, Row> _rows;
        private List<ExcelCellEntry> _entries;
        private Dictionary<uint, (int Column, int Row, string Text)> _sharedMasters;
        private bool _prepared;
        private int _maxRow = -1;
        private int _maxColumn = -1;

        internal ExcelSheet(Sheet entry, WorksheetPart part, int position, int index)
        {
            Entry = entry;
            Part = part;
            Position = position;
            Index = index;
        }

        public string Name => Entry.Name?.Value ?? string.Empty;

        public int Index { get; }

        public int Position { get; }

        public bool IsHidden => Entry.State != null && Entry.State.InnerText != "visible";

        public Sheet Entry { get; }

        public WorksheetPart Part { get; }

        public Worksheet Worksheet => Part.Worksheet;

        public SheetData Data => Worksheet?.GetFirstChild<SheetData>();

        public IReadOnlyList<ExcelCellEntry> Entries
        {
            get
            {
                EnsureIndex();
                return _entries ??= BuildEntries();
            }
        }

        public int MaxRow
        {
            get
            {
                EnsureIndex();
                return _maxRow;
            }
        }

        public int MaxColumn
        {
            get
            {
                EnsureIndex();
                return _maxColumn;
            }
        }

        public CellRange UsedRange
        {
            get
            {
                CellRange used = null;
                foreach (var entry in Entries)
                {
                    if (!HasContent(entry.Cell))
                        continue;

                    var single = CellRange.Of(entry.Address);
                    used = used == null ? single : used.Union(single);
                }

                return used;
            }
        }

        public double? DefaultColumnWidth
        {
            get
            {
                var format = Worksheet?.SheetFormatProperties;
                if (format?.DefaultColumnWidth?.Value is double width)
                    return width;

                if (format?.BaseColumnWidth?.Value is uint baseWidth)
                    return baseWidth + 0.71;

                return null;
            }
        }

        public static bool HasContent(Cell cell) =>
            cell != null && (cell.CellFormula != null || cell.InlineString != null || !string.IsNullOrEmpty(cell.CellValue?.Text));

        public Cell FindCell(int column, int row)
        {
            EnsureIndex();
            return _cells.TryGetValue(Key(column, row), out var cell) ? cell : null;
        }

        public string FormulaOf(Cell cell, int column, int row)
        {
            var formula = cell?.CellFormula;
            if (formula == null)
                return null;

            var text = formula.Text;
            if (!string.IsNullOrEmpty(text))
                return text;

            if (formula.FormulaType?.InnerText != "shared" || formula.SharedIndex == null)
                return null;

            _sharedMasters ??= BuildSharedMasters();
            if (!_sharedMasters.TryGetValue(formula.SharedIndex.Value, out var master))
                return null;

            return FormulaText.Shift(master.Text, row - master.Row, column - master.Column);
        }

        public double? ColumnWidth(int column, out bool hidden)
        {
            hidden = false;
            var number = (uint)(column + 1);
            foreach (var definition in Worksheet?.GetFirstChild<Columns>()?.Elements<Column>() ?? Enumerable.Empty<Column>())
            {
                if (definition.Min?.Value <= number && definition.Max?.Value >= number)
                {
                    hidden = definition.Hidden?.Value == true;
                    return definition.Width?.Value;
                }
            }

            return null;
        }

        public IReadOnlyList<CellRange> MergedRanges()
        {
            var merges = Worksheet?.Elements<MergeCells>().FirstOrDefault();
            if (merges == null)
                return Array.Empty<CellRange>();

            var ranges = new List<CellRange>();
            foreach (var merge in merges.Elements<MergeCell>())
            {
                if (CellRange.TryParse(merge.Reference?.Value, out var range))
                    ranges.Add(range);
            }

            return ranges;
        }

        internal void PrepareForWrite()
        {
            if (_prepared)
                return;

            var data = EnsureData();
            var nextRow = 1u;
            foreach (var row in data.Elements<Row>())
            {
                var index = row.RowIndex?.Value ?? 0u;
                if (index < 1)
                {
                    index = nextRow;
                    row.RowIndex = index;
                }

                nextRow = index + 1;
                var nextColumn = 0;
                foreach (var cell in row.Elements<Cell>())
                {
                    var column = CellAddress.TryParse(cell.CellReference?.Value, out var address) ? address.Column : nextColumn;
                    var reference = new CellAddress(column, (int)index - 1).ToString();
                    if (cell.CellReference?.Value != reference)
                        cell.CellReference = reference;

                    nextColumn = column + 1;
                }
            }

            Invalidate();
            ExpandSharedFormulas();
            _prepared = true;
        }

        internal Cell GetOrCreateCell(int column, int row)
        {
            var existing = FindCell(column, row);
            if (existing != null)
                return existing;

            var rowElement = GetOrCreateRow(row);
            var cell = new Cell { CellReference = new CellAddress(column, row).ToString() };
            var style = InheritedStyle(rowElement, column);
            if (style != 0)
                cell.StyleIndex = style;

            var before = rowElement.Elements<Cell>().FirstOrDefault(c => ColumnOf(c) > column);
            if (before != null)
                rowElement.InsertBefore(cell, before);
            else
                rowElement.Append(cell);

            _cells[Key(column, row)] = cell;
            _entries = null;
            _maxRow = Math.Max(_maxRow, row);
            _maxColumn = Math.Max(_maxColumn, column);
            return cell;
        }

        internal void ReplaceCell(int column, int row, Cell replacement)
        {
            var existing = GetOrCreateCell(column, row);
            replacement.CellReference = new CellAddress(column, row).ToString();
            existing.InsertBeforeSelf(replacement);
            existing.Remove();
            _cells[Key(column, row)] = replacement;
            _entries = null;
        }

        internal void RemoveCell(int column, int row)
        {
            var cell = FindCell(column, row);
            if (cell == null)
                return;

            var rowElement = cell.Parent as Row;
            cell.Remove();
            _cells.Remove(Key(column, row));
            _entries = null;
            if (rowElement != null && !rowElement.Elements<Cell>().Any() && IsPlainRow(rowElement))
            {
                rowElement.Remove();
                _rows.Remove(row);
            }
        }

        internal void ClearContent(int column, int row)
        {
            var cell = FindCell(column, row);
            if (cell == null)
                return;

            cell.CellFormula = null;
            cell.CellValue = null;
            cell.InlineString = null;
            cell.DataType = null;
            if (cell.StyleIndex == null || cell.StyleIndex.Value == 0)
                RemoveCell(column, row);
        }

        internal void EnsureColumnWidth(int column, double width)
        {
            width = Math.Min(255, Math.Ceiling(width * 2) / 2);
            var current = ColumnWidth(column, out _) ?? DefaultColumnWidth ?? 8.43;
            if (current >= width)
                return;

            var data = EnsureData();
            var columns = Worksheet.GetFirstChild<Columns>();
            if (columns == null)
            {
                columns = new Columns();
                data.InsertBeforeSelf(columns);
            }

            var number = (uint)(column + 1);
            var existing = columns.Elements<Column>().FirstOrDefault(c => c.Min?.Value <= number && c.Max?.Value >= number);
            if (existing == null)
            {
                var created = new Column { Min = number, Max = number, Width = width, CustomWidth = true };
                var next = columns.Elements<Column>().FirstOrDefault(c => c.Min?.Value > number);
                if (next != null)
                    next.InsertBeforeSelf(created);
                else
                    columns.Append(created);

                return;
            }

            var min = existing.Min.Value;
            var max = existing.Max.Value;
            if (min < number)
            {
                var before = (Column)existing.CloneNode(true);
                before.Max = number - 1;
                existing.InsertBeforeSelf(before);
            }

            if (max > number)
            {
                var after = (Column)existing.CloneNode(true);
                after.Min = number + 1;
                existing.InsertAfterSelf(after);
            }

            existing.Min = number;
            existing.Max = number;
            existing.Width = width;
            existing.CustomWidth = true;
        }

        internal void UpdateDimension()
        {
            var worksheet = Worksheet;
            if (worksheet == null)
                return;

            CellRange bounds = null;
            foreach (var entry in Entries)
            {
                var single = CellRange.Of(entry.Address);
                bounds = bounds == null ? single : bounds.Union(single);
            }

            var reference = bounds?.ToString() ?? "A1";
            if (worksheet.SheetDimension == null)
                worksheet.SheetDimension = new SheetDimension { Reference = reference };
            else
                worksheet.SheetDimension.Reference = reference;
        }

        internal void Invalidate()
        {
            _cells = null;
            _rows = null;
            _entries = null;
            _sharedMasters = null;
        }

        private static long Key(int column, int row) => ((long)row * CellAddress.MaxColumns) + column;

        private static int ColumnOf(Cell cell) => CellAddress.TryParse(cell.CellReference?.Value, out var address) ? address.Column : int.MaxValue;

        private static bool IsPlainRow(Row row) =>
            row.StyleIndex == null && row.Height == null && row.Hidden == null && row.CustomHeight == null && row.OutlineLevel == null && row.CustomFormat == null;

        private void EnsureIndex()
        {
            if (_cells != null)
                return;

            _cells = new Dictionary<long, Cell>();
            _rows = new Dictionary<int, Row>();
            _entries = null;
            _maxRow = -1;
            _maxColumn = -1;
            var data = Data;
            if (data == null)
                return;

            var nextRow = 0;
            foreach (var row in data.Elements<Row>())
            {
                var rowIndex = row.RowIndex?.Value is uint index && index >= 1 ? (int)index - 1 : nextRow;
                nextRow = rowIndex + 1;
                _rows.TryAdd(rowIndex, row);
                var nextColumn = 0;
                foreach (var cell in row.Elements<Cell>())
                {
                    var column = CellAddress.TryParse(cell.CellReference?.Value, out var address) ? address.Column : nextColumn;
                    nextColumn = column + 1;
                    if (_cells.TryAdd(Key(column, rowIndex), cell))
                    {
                        _maxRow = Math.Max(_maxRow, rowIndex);
                        _maxColumn = Math.Max(_maxColumn, column);
                    }
                }
            }
        }

        private List<ExcelCellEntry> BuildEntries()
        {
            var entries = new List<ExcelCellEntry>(_cells.Count);
            var data = Data;
            if (data == null)
                return entries;

            var nextRow = 0;
            foreach (var row in data.Elements<Row>())
            {
                var rowIndex = row.RowIndex?.Value is uint index && index >= 1 ? (int)index - 1 : nextRow;
                nextRow = rowIndex + 1;
                var nextColumn = 0;
                foreach (var cell in row.Elements<Cell>())
                {
                    var column = CellAddress.TryParse(cell.CellReference?.Value, out var address) ? address.Column : nextColumn;
                    nextColumn = column + 1;
                    if (_cells.TryGetValue(Key(column, rowIndex), out var indexed) && ReferenceEquals(indexed, cell))
                        entries.Add(new ExcelCellEntry(column, rowIndex, cell));
                }
            }

            return entries;
        }

        private Dictionary<uint, (int Column, int Row, string Text)> BuildSharedMasters()
        {
            var masters = new Dictionary<uint, (int Column, int Row, string Text)>();
            foreach (var entry in Entries)
            {
                var formula = entry.Cell.CellFormula;
                if (formula?.FormulaType?.InnerText == "shared" && formula.SharedIndex != null && !string.IsNullOrEmpty(formula.Text))
                    masters.TryAdd(formula.SharedIndex.Value, (entry.Column, entry.Row, formula.Text));
            }

            return masters;
        }

        private void ExpandSharedFormulas()
        {
            var shared = Entries.Where(e => e.Cell.CellFormula?.FormulaType?.InnerText == "shared").ToList();
            if (shared.Count == 0)
                return;

            var texts = shared.Select(e => FormulaOf(e.Cell, e.Column, e.Row)).ToList();
            for (var i = 0; i < shared.Count; i++)
            {
                var cell = shared[i].Cell;
                if (string.IsNullOrEmpty(texts[i]))
                {
                    cell.CellFormula = null;
                    continue;
                }

                var formula = cell.CellFormula;
                formula.FormulaType = null;
                formula.SharedIndex = null;
                formula.Reference = null;
                formula.Text = texts[i];
            }

            _sharedMasters = null;
        }

        private SheetData EnsureData()
        {
            var data = Data;
            if (data != null)
                return data;

            data = new SheetData();
            var worksheet = Worksheet;
            OpenXmlElement anchor = (OpenXmlElement)worksheet.GetFirstChild<Columns>()
                ?? (OpenXmlElement)worksheet.SheetFormatProperties
                ?? (OpenXmlElement)worksheet.SheetViews
                ?? (OpenXmlElement)worksheet.SheetDimension
                ?? worksheet.SheetProperties;
            if (anchor != null)
                anchor.InsertAfterSelf(data);
            else
                worksheet.PrependChild(data);

            Invalidate();
            return data;
        }

        private Row GetOrCreateRow(int row)
        {
            EnsureIndex();
            if (_rows.TryGetValue(row, out var existing))
                return existing;

            var data = EnsureData();
            EnsureIndex();
            var element = new Row { RowIndex = (uint)(row + 1) };
            var last = data.LastChild as Row;
            if (last == null || (last.RowIndex?.Value ?? 0) < row + 1)
            {
                data.Append(element);
            }
            else
            {
                var before = data.Elements<Row>().FirstOrDefault(r => (r.RowIndex?.Value ?? 0) > row + 1);
                if (before != null)
                    data.InsertBefore(element, before);
                else
                    data.Append(element);
            }

            _rows[row] = element;
            return element;
        }

        private uint InheritedStyle(Row row, int column)
        {
            if (row.CustomFormat?.Value == true && row.StyleIndex != null)
                return row.StyleIndex.Value;

            var number = (uint)(column + 1);
            foreach (var definition in Worksheet?.GetFirstChild<Columns>()?.Elements<Column>() ?? Enumerable.Empty<Column>())
            {
                if (definition.Min?.Value <= number && definition.Max?.Value >= number)
                    return definition.Style?.Value ?? 0;
            }

            return 0;
        }
    }
}
