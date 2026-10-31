using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal static class SheetNames
    {
        private const int MaxLength = 31;

        private static readonly char[] s_invalid = { ':', '\\', '/', '?', '*', '[', ']' };

        public static string Check(ExcelBook book, string name, Sheet except = null)
        {
            var value = (name ?? string.Empty).Trim();
            if (value.Length == 0)
                throw new ToolInputException("시트 이름을 알려 주세요.");

            if (value.Length > MaxLength)
                throw new ToolInputException("시트 이름은 31자까지만 쓸 수 있어요.");

            if (value.IndexOfAny(s_invalid) >= 0)
                throw new ToolInputException("시트 이름에는 : \\ / ? * [ ] 를 쓸 수 없어요.");

            if (value.StartsWith("'", StringComparison.Ordinal) || value.EndsWith("'", StringComparison.Ordinal))
                throw new ToolInputException("시트 이름은 작은따옴표(')로 시작하거나 끝날 수 없어요.");

            if (string.Equals(value, "History", StringComparison.OrdinalIgnoreCase))
                throw new ToolInputException("History는 엑셀이 따로 쓰는 이름이라 시트 이름으로 쓸 수 없어요.");

            if (Entries(book).Any(e => e != except && string.Equals(e.Name?.Value, value, StringComparison.OrdinalIgnoreCase)))
                throw new ToolInputException($"같은 이름의 시트가 벌써 있어요: ‘{value}’. 다른 이름을 골라 주세요.");

            return value;
        }

        public static string CopyName(ExcelBook book, string name)
        {
            var taken = new HashSet<string>(Entries(book).Select(e => e.Name?.Value ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            for (var number = 2; ; number++)
            {
                var suffix = " (" + number.ToString(CultureInfo.InvariantCulture) + ")";
                var stem = name.Length + suffix.Length > MaxLength ? name.Substring(0, MaxLength - suffix.Length).TrimEnd() : name;
                var candidate = stem + suffix;
                if (!taken.Contains(candidate))
                    return candidate;
            }
        }

        private static IEnumerable<Sheet> Entries(ExcelBook book) =>
            book.WorkbookPart?.Workbook?.Sheets?.Elements<Sheet>() ?? Enumerable.Empty<Sheet>();
    }
}
