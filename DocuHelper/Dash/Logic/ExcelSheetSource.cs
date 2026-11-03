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

        public string Range { get; set; }

        public override DashTable Read(Docu docu)
        {
            if (docu is not ExcelDocu excel)
                throw new DashException($"‘{File}’은 엑셀 파일이 아니에요.");

            var book = new ExcelBook(excel.Document);
            var sheet = FindSheet(book);
            var range = FindRange(sheet);
            var headerRow = Math.Clamp(Header - 1, range.Top, range.Bottom);

            var headers = new List<string>();
            for (var column = range.Left; column <= range.Right; column++)
            {
                var cell = sheet.FindCell(column, headerRow);
                headers.Add(book.DisplayText(book.ReadValue(cell), book.StyleOf(cell)));
            }

            var table = new DashTable(DashTables.Names(headers, i => CellAddress.ColumnName(range.Left + i) + "열"));
            for (var row = headerRow + 1; row <= range.Bottom; row++)
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
