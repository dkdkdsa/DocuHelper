using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal sealed class RangeCopier
    {
        private const int MaxCells = 20000;

        private readonly ExcelBook _book;
        private readonly ExcelSheet _source;
        private readonly CellRange _range;
        private readonly ExcelSheet _target;
        private readonly PasteKind _paste;
        private readonly PasteOperation _operation;
        private readonly bool _skipBlanks;
        private readonly bool _transpose;
        private readonly bool _move;
        private readonly bool _sameSheet;
        private readonly CellAddress _start;
        private Dictionary<CellAddress, double> _combined = new();

        public RangeCopier(ExcelBook book, ExcelSheet source, CellRange range, ExcelSheet target, CellAddress start, PasteKind paste, PasteOperation operation, bool skipBlanks, bool transpose, bool move)
        {
            _book = book;
            _source = source;
            _range = range;
            _target = target;
            _start = start;
            _paste = paste;
            _operation = operation;
            _skipBlanks = skipBlanks;
            _transpose = transpose;
            _move = move;
            _sameSheet = source == target;

            var width = transpose ? range.RowCount : range.ColumnCount;
            var height = transpose ? range.ColumnCount : range.RowCount;
            if (start.Column + width > CellAddress.MaxColumns || start.Row + height > CellAddress.MaxRows)
                throw new ToolInputException($"{start}부터 붙여 넣으면 시트 끝을 넘어가요. 붙여 넣을 자리를 더 앞쪽으로 정해 주세요.");

            Destination = new CellRange(start.Column, start.Row, start.Column + width - 1, start.Row + height - 1);
        }

        public CellRange Destination { get; }

        public bool IsSamePlace => _sameSheet && !_transpose && _paste is not (PasteKind.Values or PasteKind.ValuesAndFormats) && _range.Left == Destination.Left && _range.Top == Destination.Top;

        private bool WritesValues => _paste is PasteKind.All or PasteKind.Formulas or PasteKind.Values or PasteKind.ValuesAndFormats;

        private bool CarriesFormats => _paste is PasteKind.All or PasteKind.Formats or PasteKind.ValuesAndFormats;

        private bool TouchesCells => WritesValues || _paste == PasteKind.Formats;

        private bool KeepsFormulas => (_paste is PasteKind.All or PasteKind.Formulas) && !_transpose;

        public void Check()
        {
            if (_range.CellCount > MaxCells)
                throw new ToolInputException($"한 번에 {MaxCells}칸까지만 옮길 수 있어요. 범위를 나눠서 알려 주세요.");

            CheckCombination();
            if (!TouchesCells)
                return;

            CheckPivots();
            CheckTables();
            CheckArrays();
            CheckMerges();
            var items = Collect(false);
            CheckMergedWrites(items);
            if (_operation != PasteOperation.None)
                _combined = Combine(items);
        }

        public IReadOnlyDictionary<CellAddress, string> PendingFormulas()
        {
            var pending = new Dictionary<CellAddress, string>();
            if (_move || !KeepsFormulas)
                return pending;

            foreach (var item in Collect(false))
            {
                if (_skipBlanks && !item.HasContent)
                    continue;

                if (item.Formula != null)
                    pending[item.To] = Shifted(item);
                else if (_target.FindCell(item.To.Column, item.To.Row)?.CellFormula != null)
                    pending[item.To] = null;
            }

            return pending.Values.Any(v => v != null) ? pending : new Dictionary<CellAddress, string>();
        }

        public Outcome Apply()
        {
            _source.PrepareForWrite();
            _target.PrepareForWrite();
            var outcome = new Outcome();
            if (_paste == PasteKind.ColumnWidths)
            {
                ApplyWidths(outcome);
                return outcome;
            }

            var items = Collect(true);
            var merges = _paste is PasteKind.All or PasteKind.Formats or PasteKind.ValuesAndFormats
                ? _source.MergedRanges().Where(_range.Contains).ToList()
                : new List<CellRange>();
            var columnDelta = Destination.Left - _range.Left;
            var rowDelta = Destination.Top - _range.Top;

            var rules = _transpose
                ? new RangeRules()
                : RangeRules.Capture(_source, _range, columnDelta, rowDelta, _paste is PasteKind.All or PasteKind.Validation, CarriesFormats, _paste == PasteKind.All);
            outcome.Rules = rules;

            if (_move)
            {
                foreach (var item in items)
                {
                    _source.RemoveCell(item.From.Column, item.From.Row);
                    outcome.Written.Add(new CellKey(_source.Index, item.From.Column, item.From.Row));
                }

                _source.RemoveMerges(_range);
                RangeRules.Remove(_source, _range, true, true, true);
            }

            if (_paste is PasteKind.All or PasteKind.Validation)
                RangeRules.Remove(_target, Destination, true, false, false);

            if (CarriesFormats)
                RangeRules.Remove(_target, Destination, false, true, false);

            if (_paste == PasteKind.All)
                RangeRules.Remove(_target, Destination, false, false, true);

            if (TouchesCells)
            {
                foreach (var item in items)
                {
                    if (_skipBlanks && !item.HasContent)
                        continue;

                    if (Write(item, outcome))
                    {
                        var key = new CellKey(_target.Index, item.To.Column, item.To.Row);
                        outcome.Written.Add(key);
                        outcome.Cells++;
                        if (item.Formula != null && KeepsFormulas)
                            outcome.Formulas.Add(key);
                        else if (item.Formula != null && item.Value.IsBlank)
                            outcome.NoResult++;
                    }
                }

                if (CarriesFormats)
                {
                    _target.RemoveMerges(Destination);
                    foreach (var merge in merges)
                        _target.AddMerge(Mapped(merge));
                }
            }

            rules.Place(_target);
            for (var row = Destination.Top; row <= Destination.Bottom; row++)
            {
                var element = _target.FindRow(row);
                if (element != null)
                    element.Spans = null;
            }

            outcome.Comments = HasCommentsOrDrawings();
            _source.UpdateDimension();
            _target.UpdateDimension();
            return outcome;
        }

        public CellAddress Map(CellAddress from)
        {
            var row = from.Row - _range.Top;
            var column = from.Column - _range.Left;
            return _transpose ? new CellAddress(_start.Column + row, _start.Row + column) : new CellAddress(_start.Column + column, _start.Row + row);
        }

        private CellRange Mapped(CellRange range)
        {
            var first = Map(range.Start);
            var last = Map(range.End);
            return new CellRange(first.Column, first.Row, last.Column, last.Row);
        }

        private void CheckCombination()
        {
            if (_move)
            {
                if (_paste != PasteKind.All)
                    throw new ToolInputException("자른 칸은 붙여넣기 방식을 고를 수 없어요. 값이나 서식만 붙이려면 자르지 말고 복사해서 붙여 주세요.");

                if (_transpose)
                    throw new ToolInputException("자른 칸은 행과 열을 바꿔 넣을 수 없어요. 복사해서 붙여 주세요.");

                if (_operation != PasteOperation.None || _skipBlanks)
                    throw new ToolInputException("자른 칸은 연산이나 빈 칸 건너뛰기와 함께 붙여 넣을 수 없어요. 복사해서 붙여 주세요.");
            }

            if (_operation != PasteOperation.None && _paste is PasteKind.Formats or PasteKind.ColumnWidths or PasteKind.Validation)
                throw new ToolInputException("연산은 값이나 수식을 붙여 넣을 때만 쓸 수 있어요. paste를 all, formulas, values, values_and_formats 중에서 골라 주세요.");

            if (_paste == PasteKind.ColumnWidths && _transpose)
                throw new ToolInputException("행과 열을 바꿔 넣을 때는 열 너비를 따로 붙여 넣을 수 없어요.");
        }

        private void CheckPivots()
        {
            void Check(ExcelSheet sheet, CellRange area, string what)
            {
                foreach (var pivot in sheet.Part.PivotTableParts)
                {
                    if (CellRange.TryParse(pivot.PivotTableDefinition?.Location?.Reference?.Value, out var location) && location.Intersects(area))
                        throw new ToolInputException($"피벗 표({location})와 겹쳐서 {what} 수 없어요. 피벗 표 밖으로 정해 주세요.");
                }
            }

            Check(_target, Destination, "붙여 넣을");
            if (_move)
                Check(_source, _range, "잘라 옮길");
        }

        private void CheckTables()
        {
            if (WritesValues)
            {
                foreach (var table in ExcelTables.On(_target))
                {
                    if (table.Range.Intersects(Destination) && (table.HeaderRange?.Intersects(Destination) == true || table.TotalsRange?.Intersects(Destination) == true))
                        throw new ToolInputException($"‘{table.Name}’ 표의 머리글 줄이나 요약 줄에는 붙여 넣을 수 없어요. 표 안쪽 칸으로 붙여 넣을 자리를 정해 주세요.");
                }
            }

            if (_move)
            {
                foreach (var table in ExcelTables.On(_source))
                {
                    if (table.Range.Intersects(_range) && (table.HeaderRange?.Intersects(_range) == true || table.TotalsRange?.Intersects(_range) == true))
                        throw new ToolInputException($"‘{table.Name}’ 표의 머리글 줄이나 요약 줄은 잘라 옮길 수 없어요. 복사해서 붙여 주세요.");
                }
            }
        }

        private void CheckArrays()
        {
            if (KeepsFormulas || _move)
            {
                foreach (var block in ArrayBlocks(_source))
                {
                    if (block.Intersects(_range) && !_range.Contains(block))
                        throw new ToolInputException($"{block} 칸들은 하나로 묶인 배열 수식이라 일부만 {(_move ? "잘라 옮길" : "복사할")} 수 없어요. 배열 수식 전체가 들어가게 범위를 넓혀 주세요.");
                }
            }

            if (!WritesValues)
                return;

            foreach (var block in ArrayBlocks(_target))
            {
                if (!block.Intersects(Destination) || Destination.Contains(block))
                    continue;

                if (_move && _sameSheet && _range.Contains(block))
                    continue;

                throw new ToolInputException($"붙여 넣을 자리에 {block} 칸으로 묶인 배열 수식의 일부가 걸쳐 있어서 덮어쓸 수 없어요.");
            }
        }

        private void CheckMerges()
        {
            if (_move)
            {
                var cut = _source.MergedRanges().FirstOrDefault(m => m.Intersects(_range) && !_range.Contains(m));
                if (cut != null)
                    throw new ToolInputException($"합쳐진 칸({cut})의 일부만 잘라 옮길 수 없어요. 범위를 {_range.Union(cut)}로 넓혀 주세요.");
            }

            if (!CarriesFormats)
                return;

            foreach (var merge in _target.MergedRanges())
            {
                if (!merge.Intersects(Destination) || Destination.Contains(merge))
                    continue;

                if (_move && _sameSheet && _range.Contains(merge))
                    continue;

                throw new ToolInputException($"붙여 넣을 자리가 합쳐진 칸({merge})의 일부만 덮고 있어요. 합쳐진 칸 전체가 들어가게 붙여 넣을 자리를 옮기거나 merge_cells의 unmerge로 먼저 풀어 주세요.");
            }
        }

        private void CheckMergedWrites(List<Item> items)
        {
            if (_paste is not (PasteKind.Formulas or PasteKind.Values))
                return;

            var merges = _target.MergedRanges().Where(m => m.Intersects(Destination)).ToList();
            if (merges.Count == 0)
                return;

            foreach (var item in items)
            {
                if (!item.HasContent)
                    continue;

                var merge = merges.FirstOrDefault(m => m.Contains(item.To.Column, item.To.Row));
                if (merge != null && !merge.Start.Equals(item.To))
                    throw new ToolInputException($"{item.To} 칸은 {merge}로 합쳐진 칸 안에 가려져 있어서 값을 붙여 넣을 수 없어요. 합쳐진 칸의 맨 왼쪽 위 {merge.Start} 칸으로 붙여 넣을 자리를 정해 주세요.");
            }
        }

        private Dictionary<CellAddress, double> Combine(List<Item> items)
        {
            var combined = new Dictionary<CellAddress, double>();
            foreach (var item in items)
            {
                if (!item.HasContent)
                    continue;

                if (!item.Value.IsNumber)
                    throw new ToolInputException($"{item.From} 칸이 숫자가 아니라서 연산해서 붙여 넣을 수 없어요. 숫자가 든 칸만 골라 주세요.");

                if (_operation == PasteOperation.Divide && item.Value.Number == 0)
                    throw new ToolInputException($"{item.From} 칸 값이 0이라서 나눌 수 없어요.");

                var cell = _target.FindCell(item.To.Column, item.To.Row);
                if (cell?.CellFormula != null)
                    throw new ToolInputException($"{item.To} 칸에 수식이 있어서 연산해서 붙여 넣을 수 없어요. 수식 칸은 빼고 붙여 넣을 자리를 정해 주세요.");

                var current = _book.ReadValue(cell);
                if (!current.IsBlank && !current.IsNumber)
                    continue;

                var left = current.IsNumber ? current.Number : 0;
                var right = item.Value.Number;
                combined[item.To] = _operation switch
                {
                    PasteOperation.Add => left + right,
                    PasteOperation.Subtract => left - right,
                    PasteOperation.Multiply => left * right,
                    _ => left / right,
                };
            }

            return combined;
        }

        private List<Item> Collect(bool clone)
        {
            var items = new List<Item>((int)_range.CellCount);
            for (var row = _range.Top; row <= _range.Bottom; row++)
            {
                for (var column = _range.Left; column <= _range.Right; column++)
                {
                    var from = new CellAddress(column, row);
                    var item = new Item { From = from, To = Map(from), Value = ExcelValue.Blank };
                    var cell = _source.FindCell(column, row);
                    if (cell != null)
                    {
                        var type = cell.CellFormula?.FormulaType?.InnerText;
                        item.Cell = clone ? (Cell)cell.CloneNode(true) : cell;
                        item.Style = cell.StyleIndex?.Value ?? 0;
                        item.HasContent = ExcelSheet.HasContent(cell);
                        item.Formula = cell.CellFormula == null ? null : _source.FormulaOf(cell, column, row) ?? string.Empty;
                        item.Array = type == "array" || type == "dataTable";
                        item.Value = _book.ReadValue(cell);
                    }

                    items.Add(item);
                }
            }

            return items;
        }

        private string Shifted(Item item)
        {
            if (_move || item.Formula.Length == 0)
                return item.Formula;

            try
            {
                FormulaText.TryShift(item.Formula, item.To.Row - item.From.Row, item.To.Column - item.From.Column, out var shifted);
                return shifted;
            }
            catch (FormulaException)
            {
                return item.Formula;
            }
        }

        private string Qualified(string formula)
        {
            try
            {
                return FormulaText.Replace(formula, reference =>
                {
                    var local = reference.Sheet != null && string.Equals(reference.Sheet.Trim(), _target.Name, System.StringComparison.OrdinalIgnoreCase);
                    if (reference.Sheet != null && !local)
                        return null;

                    var copy = reference.Clone();
                    copy.Sheet = local ? null : _source.Name;
                    copy.SheetPrefix = null;
                    return copy.Render();
                });
            }
            catch (FormulaException)
            {
                return formula;
            }
        }

        private Cell Build(Item item, bool keepFormula)
        {
            var cell = item.Cell;
            if (item.Formula == null)
                return cell;

            if (!keepFormula)
            {
                AsValue(cell, item.Value);
                return cell;
            }

            var formula = cell.CellFormula;
            formula.Text = _move && !_sameSheet ? Qualified(item.Formula) : Shifted(item);
            if (item.Array)
            {
                if (CellRange.TryParse(formula.Reference?.Value, out var block))
                {
                    var delta = (Columns: item.To.Column - item.From.Column, Rows: item.To.Row - item.From.Row);
                    formula.Reference = new CellRange(block.Left + delta.Columns, block.Top + delta.Rows, block.Right + delta.Columns, block.Bottom + delta.Rows).ToString();
                }
            }
            else
            {
                formula.FormulaType = null;
                formula.SharedIndex = null;
                formula.Reference = null;
            }

            if (!_move)
            {
                cell.CellValue = null;
                cell.DataType = null;
            }

            return cell;
        }

        private void AsValue(Cell cell, ExcelValue value)
        {
            cell.CellFormula = null;
            cell.InlineString = null;
            switch (value.Kind)
            {
                case ExcelValueKind.Number:
                    ExcelBook.SetNumber(cell, value.Number);
                    break;
                case ExcelValueKind.Text:
                    _book.SetText(cell, value.Text);
                    break;
                case ExcelValueKind.Boolean:
                    ExcelBook.SetBoolean(cell, value.Boolean);
                    break;
                case ExcelValueKind.Error:
                    cell.CellValue = new CellValue(value.Text);
                    cell.DataType = CellValues.Error;
                    break;
                default:
                    cell.CellValue = null;
                    cell.DataType = null;
                    break;
            }
        }

        private bool Write(Item item, Outcome outcome)
        {
            var to = item.To;
            if (_operation != PasteOperation.None)
            {
                if (!_combined.TryGetValue(to, out var number))
                {
                    outcome.Skipped++;
                    return false;
                }

                var target = _target.GetOrCreateCell(to.Column, to.Row);
                if (CarriesFormats)
                    target.StyleIndex = item.Style == 0 ? null : item.Style;

                ExcelBook.SetNumber(target, number);
                return true;
            }

            switch (_paste)
            {
                case PasteKind.Formats:
                    WriteFormat(item);
                    return true;
                case PasteKind.All:
                case PasteKind.ValuesAndFormats:
                    if (item.Cell == null)
                        _target.RemoveCell(to.Column, to.Row);
                    else
                        _target.ReplaceCell(to.Column, to.Row, Build(item, _paste == PasteKind.All && KeepsFormulas));

                    return true;
                default:
                    if (item.Cell == null)
                    {
                        _target.ClearContent(to.Column, to.Row);
                        return true;
                    }

                    var built = Build(item, _paste == PasteKind.Formulas && KeepsFormulas);
                    var cell = _target.GetOrCreateCell(to.Column, to.Row);
                    cell.RemoveAllChildren();
                    cell.DataType = null;
                    if (built.DataType != null)
                        cell.DataType = built.DataType.Value;

                    foreach (var child in built.ChildElements.ToList())
                        cell.AppendChild(child.CloneNode(true));

                    return true;
            }
        }

        private void WriteFormat(Item item)
        {
            var existing = _target.FindCell(item.To.Column, item.To.Row);
            if (existing == null && item.Style == 0)
                return;

            var cell = existing ?? _target.GetOrCreateCell(item.To.Column, item.To.Row);
            cell.StyleIndex = item.Style == 0 ? null : item.Style;
            if (item.Style == 0 && !ExcelSheet.HasContent(cell))
                _target.RemoveCell(item.To.Column, item.To.Row);
        }

        private void ApplyWidths(Outcome outcome)
        {
            for (var offset = 0; offset < _range.ColumnCount; offset++)
            {
                var width = _source.StoredColumnWidth(_range.Left + offset, out _, out _);
                var value = width.HasValue && width.Value > 0 ? width.Value : _source.DefaultColumnWidth ?? 9.140625;
                _target.SetColumnWidth(Destination.Left + offset, Destination.Left + offset, value);
                outcome.Widths++;
            }
        }

        private bool HasCommentsOrDrawings()
        {
            if (!TouchesCells)
                return false;

            var comments = _source.Part.WorksheetCommentsPart?.Comments?.CommentList?.Elements<Comment>() ?? Enumerable.Empty<Comment>();
            if (comments.Any(c => CellAddress.TryParse(c.Reference?.Value, out var at) && _range.Contains(at.Column, at.Row)))
                return true;

            return _source.Part.DrawingsPart != null;
        }

        private static List<CellRange> ArrayBlocks(ExcelSheet sheet)
        {
            var blocks = new List<CellRange>();
            foreach (var entry in sheet.Entries)
            {
                var formula = entry.Cell.CellFormula;
                var type = formula?.FormulaType?.InnerText;
                if ((type == "array" || type == "dataTable") && CellRange.TryParse(formula.Reference?.Value, out var block))
                    blocks.Add(block);
            }

            return blocks;
        }

        public sealed class Outcome
        {
            public List<CellKey> Written { get; } = new();

            public List<CellKey> Formulas { get; } = new();

            public int Cells { get; set; }

            public int Skipped { get; set; }

            public int NoResult { get; set; }

            public int Widths { get; set; }

            public bool Comments { get; set; }

            public RangeRules Rules { get; set; } = new();
        }

        private sealed class Item
        {
            public CellAddress From { get; set; }

            public CellAddress To { get; set; }

            public Cell Cell { get; set; }

            public uint Style { get; set; }

            public bool HasContent { get; set; }

            public string Formula { get; set; }

            public bool Array { get; set; }

            public ExcelValue Value { get; set; }
        }
    }
}
