using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Tools.Excel;
using DocumentFormat.OpenXml.Packaging;

namespace DocuHelperEditor.State
{
    public sealed class WordMailRecipients
    {
        private const int MaxRecipients = 1000;

        public WordMailRecipients(string fileName, IReadOnlyList<string> fields, IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
        {
            FileName = fileName;
            Fields = fields;
            Rows = rows;
        }

        public string FileName { get; }

        public IReadOnlyList<string> Fields { get; }

        public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; }

        public static WordMailRecipients Read(string path, string sheetName)
        {
            using var document = SpreadsheetDocument.Open(path, false);
            var book = new ExcelBook(document);
            var sheet = string.IsNullOrWhiteSpace(sheetName) ? book.Sheets.FirstOrDefault() : book.FindSheet(sheetName.Trim());
            if (sheet == null || sheet.MaxRow < 1 || sheet.MaxColumn < 0)
                return null;

            string TextAt(int column, int row)
            {
                var cell = sheet.FindCell(column, row);
                return cell == null ? string.Empty : book.DisplayText(book.ReadValue(cell), book.StyleOf(cell)).Trim();
            }

            var columns = Enumerable.Range(0, sheet.MaxColumn + 1)
                .Select(column => (Column: column, Name: TextAt(column, 0)))
                .Where(c => c.Name.Length > 0)
                .DistinctBy(c => c.Name)
                .ToList();
            if (columns.Count == 0)
                return null;

            var rows = new List<IReadOnlyDictionary<string, string>>();
            for (var row = 1; row <= sheet.MaxRow && rows.Count < MaxRecipients; row++)
            {
                var values = columns.ToDictionary(c => c.Name, c => TextAt(c.Column, row), StringComparer.Ordinal);
                if (values.Values.All(v => v.Length == 0))
                    continue;

                rows.Add(values);
            }

            return new WordMailRecipients(System.IO.Path.GetFileName(path), columns.Select(c => c.Name).ToList(), rows);
        }
    }
}
