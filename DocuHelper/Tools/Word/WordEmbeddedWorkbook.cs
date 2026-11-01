using System;
using System.Collections.Generic;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Editors;
using DocuHelper.Tools;
using DocuHelper.Tools.Excel;

namespace DocuHelper.Tools.Word
{
    public static class WordEmbeddedWorkbook
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public static (byte[] Bytes, ExcelBook Book, string Sheet) Build(IReadOnlyList<string> categories, IReadOnlyList<WordChartSeries> series, bool numericCategories)
        {
            var docu = (ExcelDocu)new ExcelEditor().CreateBlank("chart-data.xlsx");
            var book = new ExcelBook(docu.Document);
            var sheet = book.Sheets[0];
            sheet.PrepareForWrite();
            for (var row = 0; row < categories.Count; row++)
            {
                var cell = sheet.GetOrCreateCell(0, row + 1);
                if (numericCategories)
                    ExcelBook.SetNumber(cell, double.Parse(categories[row], System.Globalization.CultureInfo.InvariantCulture));
                else
                    book.SetText(cell, categories[row]);
            }

            for (var column = 0; column < series.Count; column++)
            {
                book.SetText(sheet.GetOrCreateCell(column + 1, 0), series[column].Name);
                for (var row = 0; row < series[column].Values.Count; row++)
                    ExcelBook.SetNumber(sheet.GetOrCreateCell(column + 1, row + 1), series[column].Values[row]);
            }

            sheet.UpdateDimension();
            return (docu.ToBytes(), book, sheet.Name);
        }
    }

    public sealed record WordChartSeries(string Name, IReadOnlyList<double> Values);
}
