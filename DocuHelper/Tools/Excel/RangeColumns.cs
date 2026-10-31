using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    internal static class RangeColumns
    {
        public static int Resolve(ExcelBook book, ExcelSheet sheet, CellRange range, bool hasHeader, string text, string field, string outside)
        {
            var value = (text ?? string.Empty).Trim();
            if (value.Length == 0)
                throw new ToolInputException($"{field} 값이 비어 있어요. F처럼 열 글자로 알려 주세요.");

            if (hasHeader)
            {
                var matches = new List<int>();
                for (var column = range.Left; column <= range.Right; column++)
                {
                    if (string.Equals(HeaderText(book, sheet, column, range.Top), value, StringComparison.OrdinalIgnoreCase))
                        matches.Add(column);
                }

                if (matches.Count == 1)
                    return matches[0];

                if (matches.Count > 1)
                    throw new ToolInputException($"머리글이 ‘{value}’인 열이 {string.Join(", ", matches.Select(CellAddress.ColumnName))}열로 여러 개예요. 열 글자로 알려 주세요.");
            }

            var parsed = Parse(value, field, hasHeader);
            if (parsed < range.Left || parsed > range.Right)
                throw new ToolInputException($"{outside} 범위 {range} 안에 있어야 해요. 지금은 {CellAddress.ColumnName(parsed)}열이에요.");

            return parsed;
        }

        public static int Parse(string text, string field, bool allowHeader = false)
        {
            var value = (text ?? string.Empty).Trim().Replace("$", string.Empty);
            if (value.EndsWith("열", StringComparison.Ordinal))
                value = value.Substring(0, value.Length - 1).Trim();

            if (CellAddress.TryParseColumn(value, out var column))
                return column;

            if (CellAddress.TryParse(value, out var address))
                return address.Column;

            throw new ToolInputException(allowHeader
                ? $"{field} 값은 F처럼 열 글자나 머리글 글자로 알려 주세요. 받은 값: {text}"
                : $"{field} 값은 F처럼 열 글자로 알려 주세요. 받은 값: {text}");
        }

        public static string HeaderText(ExcelBook book, ExcelSheet sheet, int column, int row)
        {
            var cell = sheet.FindCell(column, row);
            if (cell == null)
                return string.Empty;

            return book.DisplayText(book.ReadValue(cell), book.StyleOf(cell)).Trim();
        }
    }
}
