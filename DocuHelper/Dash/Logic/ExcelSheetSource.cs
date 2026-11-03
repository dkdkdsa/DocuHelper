using System;
using System.Collections.Generic;
using DocuHelper.Dash.Runtime;
using DocuHelper.Docus;
using DocuHelper.Tools.Excel;

namespace DocuHelper.Dash.Logic
{
    public sealed class ExcelSheetSource : DashSource
    {
        public string Sheet { get; set; }

        public int Header { get; set; } = 1;

        public int? HeaderRows { get; set; }

        public string Range { get; set; }

        public override DashTable Read(Docu docu)
        {
            if (docu is not ExcelDocu excel)
                throw new DashException($"‘{File}’은 엑셀 파일이 아니에요.");

            var book = new ExcelBook(excel.Document);
            var sheet = FindSheet(book);
            var range = FindRange(sheet);
            var headerRow = Math.Clamp(Header - 1, range.Top, range.Bottom);
            var depth = HeaderRows is int rows ? Math.Clamp(rows, 1, 2) : IsTwoRowHeader(book, sheet, range, headerRow) ? 2 : 1;
            if (headerRow + depth - 1 > range.Bottom)
                depth = 1;

            var headers = new List<string>();
            for (var column = range.Left; column <= range.Right; column++)
                headers.Add(HeaderName(book, sheet, column, headerRow, depth));

            var table = new DashTable(DashTables.Names(headers, i => CellAddress.ColumnName(range.Left + i) + "열"));
            for (var row = headerRow + depth; row <= range.Bottom; row++)
            {
                var values = new object[headers.Count];
                var filled = false;
                for (var i = 0; i < values.Length; i++)
                {
                    var cell = sheet.FindCell(range.Left + i, row);
                    if (cell == null)
                        continue;

                    values[i] = DashCells.FromExcel(book.ReadValue(cell), book.StyleOf(cell).IsDate);
                    filled |= values[i] != null;
                }

                if (filled)
                    table.Rows.Add(values);
            }

            return table;
        }

        private static bool IsTwoRowHeader(ExcelBook book, ExcelSheet sheet, CellRange range, int headerRow)
        {
            if (headerRow + 1 > range.Bottom)
                return false;

            foreach (var merge in sheet.MergedRanges())
            {
                if (merge.Top != headerRow || merge.Right < range.Left || merge.Left > range.Right)
                    continue;

                if (merge.Bottom > merge.Top)
                    return true;

                for (var column = merge.Left; column <= merge.Right; column++)
                {
                    if (sheet.FindCell(column, headerRow + 1) is { } below && !string.IsNullOrWhiteSpace(DashCells.Text(book.ReadValue(below))))
                        return true;
                }
            }

            return false;
        }

        private static string HeaderName(ExcelBook book, ExcelSheet sheet, int column, int headerRow, int depth)
        {
            var top = TextAt(book, sheet, column, headerRow);
            if (depth == 1)
                return top;

            var merge = sheet.MergeAt(column, headerRow + 1);
            if (merge != null && merge.Top <= headerRow)
                return top;

            var sub = TextAt(book, sheet, column, headerRow + 1);
            if (string.IsNullOrWhiteSpace(sub))
                return top;

            return string.IsNullOrWhiteSpace(top) || string.Equals(top.Trim(), sub.Trim(), StringComparison.Ordinal) ? sub : top.Trim() + " " + sub.Trim();
        }

        private static string TextAt(ExcelBook book, ExcelSheet sheet, int column, int row)
        {
            var merge = sheet.MergeAt(column, row);
            var cell = merge == null ? sheet.FindCell(column, row) : sheet.FindCell(merge.Left, merge.Top);
            return book.DisplayText(book.ReadValue(cell), book.StyleOf(cell));
        }

        private ExcelSheet FindSheet(ExcelBook book)
        {
            if (string.IsNullOrWhiteSpace(Sheet))
                return book.Sheets.Count > 0 ? book.Sheets[0] : throw new DashException($"‘{File}’ 파일에 시트가 없어요.");

            return book.FindSheet(Sheet)
                ?? throw new DashException($"‘{File}’ 파일에서 ‘{Sheet}’ 시트를 찾을 수 없어요. 있는 시트: {book.SheetList()}");
        }

        private CellRange FindRange(ExcelSheet sheet)
        {
            CellRange range;
            if (string.IsNullOrWhiteSpace(Range))
                range = sheet.UsedRange;
            else if (!CellRange.TryParse(Range.Substring(Range.LastIndexOf('!') + 1), sheet.MaxRow, sheet.MaxColumn, out range))
                throw new DashException($"‘{Range}’은 읽을 수 없는 범위예요. ‘A1:F20’처럼 적어 주세요.");

            if (range == null)
                throw new DashException($"‘{sheet.Name}’ 시트에 내용이 없어요.");

            return new CellRange(
                range.Left,
                range.Top,
                Math.Min(range.Right, Math.Max(range.Left, sheet.MaxColumn)),
                Math.Min(range.Bottom, Math.Max(range.Top, sheet.MaxRow)));
        }
    }
}
