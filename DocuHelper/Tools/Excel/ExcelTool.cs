using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public abstract class ExcelTool : Tool
    {
        protected const int MaxCells = 20000;

        private static readonly char[] s_complexSyntax = { '[', '{' };

        public override ToolResult Execute(Docu docu, ToolInput input)
        {
            if (docu is not ExcelDocu excel || excel.Document?.WorkbookPart?.Workbook == null)
                return ToolResult.Fail("엑셀 파일이 아니라서 이 도구를 쓸 수 없어요.");

            var book = new ExcelBook(excel.Document);
            if (!IsReadOnly)
                ProtectionGuard.Check(book, Name, input);

            var result = Run(excel, book, input);
            if (!IsReadOnly && !result.IsError)
                ExcelTables.Reconcile(book);

            return result;
        }

        public static bool IsTotalLabel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var value = text.Trim();
            return value.Contains("합계", StringComparison.Ordinal)
                || value.Contains("총계", StringComparison.Ordinal)
                || value.Contains("소계", StringComparison.Ordinal)
                || value.StartsWith("total", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("grand total", StringComparison.OrdinalIgnoreCase);
        }

        protected abstract ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input);

        protected static ExcelSheet ResolveSheet(ExcelBook book, ToolInput input, string positionField, bool forWrite)
        {
            var name = input.GetOptionalString("sheet");
            if (string.IsNullOrWhiteSpace(name) && positionField != null && SplitSheet(input.GetOptionalString(positionField), out var prefix, out _))
                name = prefix;

            if (book.Sheets.Count == 0)
                throw new ToolInputException("이 파일에는 시트가 없어요.");

            if (string.IsNullOrWhiteSpace(name))
            {
                if (!forWrite || book.Sheets.Count == 1)
                    return book.Sheets[0];

                throw new ToolInputException($"어느 시트인지 sheet 값으로 알려 주세요. 있는 시트: {book.SheetList()}");
            }

            var sheet = book.FindSheet(name);
            if (sheet == null)
                throw new ToolInputException($"‘{name.Trim()}’ 시트를 찾을 수 없어요. 있는 시트: {book.SheetList()}");

            return sheet;
        }

        protected static bool SplitSheet(string text, out string sheet, out string rest)
        {
            sheet = null;
            rest = text;
            if (string.IsNullOrEmpty(text))
                return false;

            var bang = text.LastIndexOf('!');
            if (bang <= 0)
                return false;

            sheet = text.Substring(0, bang).Trim();
            rest = text.Substring(bang + 1);
            if (sheet.Length > 1 && sheet[0] == '\'' && sheet[^1] == '\'')
                sheet = sheet.Substring(1, sheet.Length - 2).Replace("''", "'");

            return sheet.Length > 0;
        }

        protected static CellRange ParseRange(ExcelSheet sheet, string text)
        {
            SplitSheet(text, out _, out var rest);
            var used = sheet.UsedRange;
            var lastRow = Math.Max(used?.Bottom ?? 0, 0);
            var lastColumn = Math.Max(used?.Right ?? 0, 0);
            if (!CellRange.TryParse(rest, lastRow, lastColumn, out var range))
                throw new ToolInputException($"알아볼 수 없는 범위예요: ‘{text}’. B2:D10처럼 알려 주세요.");

            return range;
        }

        protected static CellAddress ParseCell(string text)
        {
            SplitSheet(text, out _, out var rest);
            var value = rest?.Trim() ?? string.Empty;
            var colon = value.IndexOf(':');
            if (colon > 0)
                value = value.Substring(0, colon);

            if (!CellAddress.TryParse(value, out var address))
                throw new ToolInputException($"알아볼 수 없는 칸 주소예요: ‘{text}’. B2처럼 알려 주세요.");

            return address;
        }

        protected static string Place(CellRange range)
        {
            if (range.IsSingleCell)
                return range.ToString();

            return range.RowCount == 1 ? $"{range.Top + 1}행" : range.ToString();
        }

        protected static List<string> Anchors(ExcelSheet sheet, IEnumerable<CellAddress> cells) =>
            cells.Select(a => ExcelAnchor.Cell(sheet.Name, a)).Distinct().ToList();

        protected static CellRange Bounds(IEnumerable<CellAddress> cells)
        {
            CellRange bounds = null;
            foreach (var address in cells)
            {
                var single = CellRange.Of(address);
                bounds = bounds == null ? single : bounds.Union(single);
            }

            return bounds;
        }

        protected static bool IsTotalRow(ExcelBook book, ExcelSheet sheet, int row)
        {
            foreach (var entry in sheet.Entries)
            {
                if (entry.Row != row)
                    continue;

                var value = book.ReadValue(entry.Cell);
                if (value.IsText && value.Text.Trim().Length > 0)
                    return IsTotalLabel(value.Text);
            }

            return false;
        }

        protected static string CheckFormula(ExcelBook book, string formula, string where)
        {
            FormulaNode node;
            try
            {
                node = FormulaParser.Parse(formula);
                FormulaFunctions.Validate(node);
            }
            catch (FormulaException e)
            {
                if (formula.IndexOfAny(s_complexSyntax) >= 0 && !FormulaParser.TryParse(formula, out _))
                    return formula;

                throw new ToolInputException($"{where} 수식을 이해할 수 없어요: ={formula} ({e.Message})");
            }

            foreach (var reference in node.Descendants().Where(n => n.Kind == FormulaNodeKind.Reference).Select(n => n.Reference))
            {
                if (reference.Sheet != null && book.FindSheet(reference.Sheet) == null)
                    throw new ToolInputException($"{where} 수식에 나오는 ‘{reference.Sheet}’ 시트가 없어요. 있는 시트: {book.SheetList()}");
            }

            return FormulaText.Normalize(formula);
        }

        protected static void CheckNotCircular(string formula, ExcelSheet sheet, CellAddress address)
        {
            foreach (var reference in FormulaText.References(formula))
            {
                if (reference.IsOnSheet(sheet.Name, sheet.Name) && reference.ToFullRange().Contains(address.Column, address.Row))
                    throw new ToolInputException($"{address} 칸 수식이 가리키는 범위에 {address} 칸 자신이 들어 있어서 순환 참조가 돼요. 범위를 고쳐 주세요: ={formula}");
            }
        }

        protected static void CheckNoCycle(ExcelBook book, ExcelSheet sheet, IReadOnlyDictionary<CellAddress, string> pending)
        {
            const int MaxVisits = 20000;
            var existing = new Dictionary<int, HashSet<CellAddress>>();

            HashSet<CellAddress> FormulaCells(ExcelSheet target)
            {
                if (!existing.TryGetValue(target.Index, out var cells))
                {
                    cells = new HashSet<CellAddress>(target.Entries.Where(e => e.Cell.CellFormula != null).Select(e => new CellAddress(e.Column, e.Row)));
                    existing[target.Index] = cells;
                }

                return cells;
            }

            string FormulaAt((ExcelSheet Sheet, CellAddress Address) node)
            {
                if (node.Sheet == sheet && pending.TryGetValue(node.Address, out var text))
                    return text;

                var cell = node.Sheet.FindCell(node.Address.Column, node.Address.Row);
                return cell?.CellFormula == null ? null : node.Sheet.FormulaOf(cell, node.Address.Column, node.Address.Row);
            }

            IEnumerable<(ExcelSheet Sheet, CellAddress Address)> FormulasIn(ExcelSheet target, CellRange range)
            {
                var cells = FormulaCells(target);
                var inRange = range.CellCount <= cells.Count
                    ? range.Cells().Where(cells.Contains)
                    : cells.Where(a => range.Contains(a.Column, a.Row));
                foreach (var address in inRange)
                {
                    if (target != sheet || !pending.ContainsKey(address))
                        yield return (target, address);
                }

                if (target != sheet)
                    yield break;

                foreach (var pair in pending)
                {
                    if (pair.Value != null && range.Contains(pair.Key.Column, pair.Key.Row))
                        yield return (target, pair.Key);
                }
            }

            string Label((ExcelSheet Sheet, CellAddress Address) node) =>
                node.Sheet == sheet ? node.Address.ToString() : $"‘{node.Sheet.Name}’!{node.Address}";

            foreach (var start in pending.Where(p => p.Value != null).Select(p => (Sheet: sheet, Address: p.Key)))
            {
                var parents = new Dictionary<(ExcelSheet, CellAddress), (ExcelSheet, CellAddress)>();
                var queue = new Queue<(ExcelSheet Sheet, CellAddress Address)>();
                queue.Enqueue(start);
                parents[start] = start;
                while (queue.Count > 0 && parents.Count < MaxVisits)
                {
                    var node = queue.Dequeue();
                    var formula = FormulaAt(node);
                    if (string.IsNullOrEmpty(formula))
                        continue;

                    foreach (var reference in FormulaText.References(formula))
                    {
                        var target = reference.Sheet == null ? node.Sheet : book.FindSheet(reference.Sheet);
                        if (target == null)
                            continue;

                        foreach (var next in FormulasIn(target, reference.ToFullRange()))
                        {
                            if (next.Equals(start))
                            {
                                var path = new List<string>();
                                for (var step = node; ; step = parents[step])
                                {
                                    path.Add(Label(step));
                                    if (step.Equals(start))
                                        break;
                                }

                                path.Reverse();
                                path.Add(Label(start));
                                throw new ToolInputException($"{start.Address} 칸 수식이 다른 칸을 거쳐 자기 자신을 다시 가리켜서 순환 참조가 돼요: {string.Join(" → ", path)}. 수식을 고쳐 주세요.");
                            }

                            if (parents.TryAdd(next, node))
                                queue.Enqueue(next);
                        }
                    }
                }
            }
        }

        protected static void FitNumbers(ExcelBook book, ExcelSheet sheet, IEnumerable<CellAddress> cells)
        {
            var widths = new Dictionary<int, double>();
            foreach (var address in cells)
            {
                var cell = sheet.FindCell(address.Column, address.Row);
                if (cell == null)
                    continue;

                var value = book.ReadValue(cell);
                if (!value.IsNumber)
                    continue;

                var text = book.DisplayText(value, book.StyleOf(cell));
                var width = text.Sum(c => c >= 0x2E80 ? 1.8 : 1.0) + 1.5;
                if (!widths.TryGetValue(address.Column, out var current) || current < width)
                    widths[address.Column] = width;
            }

            foreach (var pair in widths)
                sheet.EnsureColumnWidth(pair.Key, pair.Value);
        }

        protected static CalculationResult Recalculate(ExcelBook book, IEnumerable<CellKey> written, bool strict = true)
        {
            var result = new WorkbookCalculator(book, written, strict).Run();
            book.MarkCalculationNeeded();
            return result;
        }

        protected static (bool Rows, int First, int Last) ParseLines(ToolInput input)
        {
            var rows = input.GetOptionalString("rows");
            var columns = input.GetOptionalString("columns");
            var hasRows = !string.IsNullOrWhiteSpace(rows);
            if (hasRows == !string.IsNullOrWhiteSpace(columns))
                throw new ToolInputException("rows와 columns 중 하나만 알려 주세요. 예: rows \"5:7\" 또는 columns \"C\".");

            var (first, last) = ParseSpan(hasRows ? rows : columns, hasRows);
            return (hasRows, first, last);
        }

        protected static string LineLabel(bool rows, int first, int last)
        {
            var start = rows ? (first + 1).ToString() : CellAddress.ColumnName(first);
            var end = rows ? (last + 1).ToString() : CellAddress.ColumnName(last);
            var unit = rows ? "행" : "열";
            return first == last ? start + unit : start + "-" + end + unit;
        }

        protected static string LineText(bool rows, int first, int last)
        {
            if (rows)
                return first == last ? $"row {first + 1}" : $"rows {first + 1}-{last + 1}";

            return first == last ? $"column {CellAddress.ColumnName(first)}" : $"columns {CellAddress.ColumnName(first)}:{CellAddress.ColumnName(last)}";
        }

        protected static List<string> LineAnchors(ExcelSheet sheet, bool rows, int first, int last)
        {
            var anchors = new List<string>();
            for (var line = first; line <= Math.Min(last, first + 499); line++)
                anchors.Add(ExcelAnchor.Cell(sheet.Name, rows ? new CellAddress(0, line) : new CellAddress(line, 0)));

            return anchors;
        }

        protected static void ShiftAnchors(Docu docu, string sheetName, LineShift shift)
        {
            foreach (var change in docu.Changes)
            {
                for (var i = change.Anchors.Count - 1; i >= 0; i--)
                {
                    if (!ExcelAnchor.TryParse(change.Anchors[i], out var sheet, out var cellRef) || sheet != sheetName || !CellAddress.TryParse(cellRef, out var address))
                        continue;

                    var mapped = shift.Map(address);
                    if (mapped == null)
                        change.Anchors.RemoveAt(i);
                    else
                        change.Anchors[i] = ExcelAnchor.Cell(sheetName, mapped.Value);
                }
            }
        }

        protected static void RenameAnchors(Docu docu, string oldName, string newName)
        {
            foreach (var change in docu.Changes)
            {
                for (var i = 0; i < change.Anchors.Count; i++)
                {
                    if (ExcelAnchor.TryParse(change.Anchors[i], out var sheet, out var cellRef) && sheet == oldName)
                        change.Anchors[i] = newName + "!" + cellRef;
                }
            }
        }

        protected static void DropSheetAnchors(Docu docu, string name)
        {
            foreach (var change in docu.Changes)
                change.Anchors.RemoveAll(a => ExcelAnchor.TryParse(a, out var sheet, out _) && sheet == name);
        }

        protected static ExcelSheet FindSheet(ExcelBook book, ToolInput input)
        {
            var name = input.GetString("sheet");
            return book.FindSheet(name) ?? throw new ToolInputException($"‘{name.Trim()}’ 시트를 찾을 수 없어요. 있는 시트: {book.SheetList()}");
        }

        protected static int? ParsePosition(ToolInput input, int count)
        {
            var position = input.GetOptionalInt("position");
            if (!position.HasValue)
                return null;

            if (position.Value < 1 || position.Value > count)
                throw new ToolInputException($"position은 1부터 {count} 사이로 알려 주세요.");

            return position.Value - 1;
        }

        private static (int First, int Last) ParseSpan(string text, bool rows)
        {
            var value = text.Trim().Replace("$", string.Empty).Replace(" ", string.Empty).TrimEnd('행', '열');
            var parts = value.Split(new[] { ':', '-', '~' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is < 1 or > 2)
                throw SpanError(text, rows);

            int Parse(string part)
            {
                if (rows && CellAddress.TryParseRow(part, out var row))
                    return row;

                if (!rows && CellAddress.TryParseColumn(part, out var column))
                    return column;

                if (CellAddress.TryParse(part, out var address))
                    return rows ? address.Row : address.Column;

                throw SpanError(text, rows);
            }

            var first = Parse(parts[0]);
            var last = parts.Length == 2 ? Parse(parts[1]) : first;
            return (Math.Min(first, last), Math.Max(first, last));
        }

        private static ToolInputException SpanError(string text, bool rows) => new(rows
            ? $"행 번호를 알아볼 수 없어요: ‘{text}’. \"5\"나 \"5:7\"처럼 알려 주세요."
            : $"열 글자를 알아볼 수 없어요: ‘{text}’. \"C\"나 \"C:D\"처럼 알려 주세요.");

        protected static string FormulaReport(CalculationResult result, IReadOnlyList<CellKey> formulas, int limit)
        {
            var text = new StringBuilder();
            if (result.Aborted)
            {
                if (formulas.Count > 0)
                    text.Append(" The workbook is too large to calculate here, so formula results are left for Excel to calculate when the file is opened.");

                return text.ToString();
            }

            if (formulas.Count > 0)
            {
                var shown = formulas.Count <= limit ? formulas.ToList() : formulas.Take(limit - 1).Append(formulas[^1]).ToList();
                text.Append(" Results: ");
                text.Append(string.Join(", ", shown.Select(k => k.Address + " = " + Show(result, k))));
                if (formulas.Count > shown.Count)
                    text.Append($" ({formulas.Count - shown.Count} more not listed)");

                text.Append('.');

                var errors = formulas.Where(k => result.Values.TryGetValue(k, out var v) && v.IsError).ToList();
                if (errors.Count > 0)
                {
                    var first = result.Values[errors[0]].Text;
                    var hint = first == ExcelValue.BadName ? "#NAME? means a function or name Excel does not know, so check the spelling." : "check the references.";
                    text.Append($" {errors.Count} formula cell(s) show an error, e.g. {errors[0].Address} = {first}; {hint}");
                }

                var unknown = formulas.Where(result.Unknown.Contains).ToList();
                if (unknown.Count > 0)
                    text.Append($" Not calculated here (unsupported function or reference), Excel calculates on open: {string.Join(", ", unknown.Take(8).Select(k => k.Address.ToString()))}{(unknown.Count > 8 ? ", …" : string.Empty)}.");
            }

            if (result.Updated > 0)
                text.Append($" {result.Updated} other formula cell(s) that depend on these were recalculated.");

            return text.ToString();
        }

        private static string Show(CalculationResult result, CellKey key)
        {
            if (!result.Values.TryGetValue(key, out var value))
                return "(not calculated)";

            return value.IsText ? "\"" + value.Text + "\"" : value.ToRawText();
        }
    }
}
