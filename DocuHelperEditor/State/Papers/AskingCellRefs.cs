using System;
using System.Collections.Generic;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public static class AskingCellRefs
    {
        public const int MaxCellsPerReference = 50;

        public static IEnumerable<SheetCell> Find(string reference, ExcelPaperModel workbook)
        {
            var text = reference?.Trim();
            if (string.IsNullOrEmpty(text))
                yield break;

            var range = text.Replace("$", string.Empty);
            Sheet sheet = workbook.ActiveSheet;
            var bang = text.LastIndexOf('!');
            if (bang >= 0)
            {
                var sheetName = Unquote(text.Substring(0, bang));
                range = text.Substring(bang + 1).Replace("$", string.Empty);
                sheet = workbook.Sheets.FirstOrDefault(s => string.Equals(s.Name, sheetName, StringComparison.OrdinalIgnoreCase));
            }

            if (sheet == null)
                yield break;

            var parts = range.Split(':');
            if (parts.Length > 2 || !SheetRef.TryParse(parts[0], out var firstColumn, out var firstRow))
                yield break;

            var lastColumn = firstColumn;
            var lastRow = firstRow;
            if (parts.Length == 2 && !SheetRef.TryParse(parts[1], out lastColumn, out lastRow))
                yield break;

            var left = Math.Min(firstColumn, lastColumn);
            var right = Math.Max(firstColumn, lastColumn);
            var top = Math.Min(firstRow, lastRow);
            var bottom = Math.Max(firstRow, lastRow);

            var count = 0;
            for (var row = top; row <= bottom && count < MaxCellsPerReference; row++)
            {
                for (var column = left; column <= right && count < MaxCellsPerReference; column++)
                {
                    count++;
                    var cell = sheet.FindCell(row, column);
                    if (cell != null)
                        yield return cell;
                }
            }
        }

        private static string Unquote(string name)
        {
            var text = name.Trim();
            if (text.Length >= 2 && text[0] == '\'' && text[^1] == '\'')
                text = text.Substring(1, text.Length - 2).Replace("''", "'");

            return text;
        }
    }
}
