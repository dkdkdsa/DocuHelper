using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public static class BlankWorkbook
    {
        public const string FirstSheetName = "시트1";

        public static byte[] Create(string sheetName = FirstSheetName)
        {
            using var stream = new MemoryStream();
            using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = document.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                var styles = workbookPart.AddNewPart<WorkbookStylesPart>();
                styles.Stylesheet = ExcelStyles.CreateDefault();

                var strings = workbookPart.AddNewPart<SharedStringTablePart>();
                strings.SharedStringTable = new SharedStringTable { Count = 0, UniqueCount = 0 };

                var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
                sheetPart.Worksheet = new Worksheet(
                    new SheetViews(new SheetView { TabSelected = true, WorkbookViewId = 0 }),
                    new SheetFormatProperties { DefaultRowHeight = 16.5 },
                    new SheetData());

                workbookPart.Workbook.Append(
                    new BookViews(new WorkbookView()),
                    new Sheets(new Sheet { Name = sheetName, SheetId = 1, Id = workbookPart.GetIdOfPart(sheetPart) }));
            }

            return stream.ToArray();
        }
    }
}
