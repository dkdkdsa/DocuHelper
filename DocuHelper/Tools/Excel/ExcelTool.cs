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

            return Run(excel, new ExcelBook(excel.Document), input);
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

        protected static CalculationResult Recalculate(ExcelBook book, IEnumerable<CellKey> written)
        {
            var result = new WorkbookCalculator(book, written).Run();
            book.MarkCalculationNeeded();
            return result;
        }

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
                    text.Append($" {errors.Count} formula cell(s) show an error, e.g. {errors[0].Address} = {result.Values[errors[0]].Text}; check the references.");

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
