using System.Collections.Generic;
using System.Linq;

namespace DocuHelper.Tools.Excel
{
    public static class WorkbookSummary
    {
        private const int MaxLength = 300;
        private const int MaxHeaderLength = 12;

        public static string Describe(ExcelBook book)
        {
            var sheets = book.Sheets;
            if (sheets.Count == 0)
                return "시트 없음";

            var details = sheets.Select(s => (Sheet: s, Used: s.UsedRange)).ToList();
            var headers = details.Select(d => d.Used == null ? new List<string>() : HeaderNames(book, d.Sheet, d.Used)).ToList();
            foreach (var limit in new[] { 8, 5, 3, 0 })
            {
                var parts = new List<string>();
                for (var i = 0; i < details.Count; i++)
                    parts.Add(Part(details[i].Sheet, details[i].Used, headers[i], limit));

                var text = Join(parts, details.Count, parts.Count);
                if (text.Length <= MaxLength)
                    return text;

                if (limit == 0)
                {
                    for (var count = parts.Count - 1; count >= 1; count--)
                    {
                        text = Join(parts, details.Count, count);
                        if (text.Length <= MaxLength)
                            return text;
                    }

                    return text.Substring(0, MaxLength - 1) + "…";
                }
            }

            return $"시트 {sheets.Count}개";
        }

        private static string Join(List<string> parts, int total, int count)
        {
            var text = $"시트 {total}개: " + string.Join(", ", parts.Take(count));
            return count < total ? text + $" 외 {total - count}개" : text;
        }

        private static string Part(ExcelSheet sheet, CellRange used, List<string> headers, int limit)
        {
            if (used == null)
                return $"‘{sheet.Name}’ 비어 있음";

            var text = $"‘{sheet.Name}’ {used}";
            if (limit == 0 || headers.Count == 0)
                return text;

            var names = headers.Take(limit).ToList();
            var list = string.Join(", ", names) + (headers.Count > names.Count ? ", …" : string.Empty);
            return text + $" (머리글: {list})";
        }

        private static List<string> HeaderNames(ExcelBook book, ExcelSheet sheet, CellRange used)
        {
            var names = new List<string>();
            foreach (var entry in sheet.Entries)
            {
                if (entry.Row > used.Top)
                    break;

                if (entry.Row != used.Top || !ExcelSheet.HasContent(entry.Cell))
                    continue;

                var value = book.ReadValue(entry.Cell);
                if (!value.IsText || entry.Cell.CellFormula != null)
                    return new List<string>();

                var name = value.Text.Trim().Replace("\n", " ");
                if (name.Length > MaxHeaderLength)
                    name = name.Substring(0, MaxHeaderLength) + "…";

                if (name.Length > 0)
                    names.Add(name);
            }

            return names.Count >= 2 ? names : new List<string>();
        }
    }
}
