using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocuHelper.Tools.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Ppt
{
    public static class PptChartWorkbook
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public const string RelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/package";

        public static byte[] Create(PptChartInfo model, IReadOnlyList<PptSeriesInfo> series)
        {
            using var stream = new MemoryStream();
            stream.Write(BlankWorkbook.Create(PptChartBuilder.SheetName));
            stream.Position = 0;
            using (var document = SpreadsheetDocument.Open(stream, true))
            {
                var data = document.WorkbookPart.WorksheetParts.First().Worksheet.GetFirstChild<SheetData>();
                var header = new Row { RowIndex = 1 };
                for (var s = 0; s < series.Count; s++)
                    header.AppendChild(TextCell(PptChartBuilder.ColumnName(s + 2) + "1", series[s].Name));

                data.AppendChild(header);
                for (var k = 0; k < model.Categories.Count; k++)
                {
                    var rowIndex = (uint)(k + 2);
                    var row = new Row { RowIndex = rowIndex };
                    row.AppendChild(TextCell("A" + rowIndex.ToString(CultureInfo.InvariantCulture), model.Categories[k]));
                    for (var s = 0; s < series.Count; s++)
                    {
                        if (k < series[s].Values.Count && series[s].Values[k] is double value)
                        {
                            row.AppendChild(new Cell
                            {
                                CellReference = PptChartBuilder.ColumnName(s + 2) + rowIndex.ToString(CultureInfo.InvariantCulture),
                                CellValue = new CellValue(value.ToString("R", CultureInfo.InvariantCulture)),
                            });
                        }
                    }

                    data.AppendChild(row);
                }

                document.WorkbookPart.Workbook.Save();
                document.WorkbookPart.WorksheetParts.First().Worksheet.Save();
            }

            return stream.ToArray();
        }

        private static Cell TextCell(string reference, string text) => new()
        {
            CellReference = reference,
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve }),
        };
    }
}
