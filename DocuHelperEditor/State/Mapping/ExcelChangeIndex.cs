using System;
using System.Collections.Generic;
using DocuHelper.Docus;
using DocuHelper.Tools.Excel;

namespace DocuHelperEditor.State.Mapping
{
    public class ExcelChangeIndex
    {
        private static readonly IReadOnlyList<(int Column, int Row)> s_noPositions = Array.Empty<(int Column, int Row)>();

        private readonly Dictionary<string, List<string>> _cells = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _sheetChanges = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, string>> _firstAnchors = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<(int Column, int Row)>> _positions = new(StringComparer.Ordinal);

        public ExcelChangeIndex(IEnumerable<DocuChange> changes)
        {
            foreach (var change in changes ?? Array.Empty<DocuChange>())
            {
                if (change == null || string.IsNullOrEmpty(change.Id))
                    continue;

                foreach (var anchor in change.Anchors)
                {
                    if (!ExcelAnchor.TryParse(anchor, out var sheet, out var cellRef))
                        continue;

                    var first = FirstAnchorsOf(sheet);
                    if (cellRef.Length == 0)
                    {
                        _sheetChanges.TryAdd(sheet, change.Id);
                        first.TryAdd(change.Id, "A1");
                        continue;
                    }

                    if (!CellAddress.TryParse(cellRef, out var address))
                        continue;

                    var key = ExcelAnchor.Cell(sheet, address);
                    if (!_cells.TryGetValue(key, out var ids))
                    {
                        ids = new List<string>();
                        _cells[key] = ids;
                        PositionsOf(sheet).Add((address.Column, address.Row));
                    }

                    if (!ids.Contains(change.Id))
                        ids.Add(change.Id);

                    first.TryAdd(change.Id, address.ToString());
                }
            }
        }

        public List<string> ChangeIds(string sheet, int column, int row) =>
            _cells.TryGetValue(ExcelAnchor.Cell(sheet, new CellAddress(column, row)), out var ids) ? new List<string>(ids) : new List<string>();

        public string SheetChange(string sheet) => _sheetChanges.TryGetValue(sheet, out var id) ? id : null;

        public Dictionary<string, string> FirstAnchors(string sheet) =>
            _firstAnchors.TryGetValue(sheet, out var anchors) ? new Dictionary<string, string>(anchors) : new Dictionary<string, string>();

        public IReadOnlyList<(int Column, int Row)> Positions(string sheet) =>
            _positions.TryGetValue(sheet, out var positions) ? positions : s_noPositions;

        private Dictionary<string, string> FirstAnchorsOf(string sheet)
        {
            if (!_firstAnchors.TryGetValue(sheet, out var anchors))
            {
                anchors = new Dictionary<string, string>();
                _firstAnchors[sheet] = anchors;
            }

            return anchors;
        }

        private List<(int Column, int Row)> PositionsOf(string sheet)
        {
            if (!_positions.TryGetValue(sheet, out var positions))
            {
                positions = new List<(int Column, int Row)>();
                _positions[sheet] = positions;
            }

            return positions;
        }
    }
}
