using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Tools.Excel;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal static class XlsxWriter
    {
        public static byte[] Write(XlsWorkbook workbook)
        {
            var sheets = workbook.Sheets;
            var firstName = sheets.Count > 0 ? sheets[0].Info.Name : BlankWorkbook.FirstSheetName;
            using var stream = new MemoryStream();
            var blank = BlankWorkbook.Create(firstName);
            stream.Write(blank, 0, blank.Length);
            stream.Position = 0;
            using (var document = SpreadsheetDocument.Open(stream, true))
            {
                Fill(document.WorkbookPart, workbook);
            }

            return stream.ToArray();
        }

        private static void Fill(WorkbookPart workbookPart, XlsWorkbook workbook)
        {
            var builder = new XlsStyleBuilder(workbook.Styles);
            var stylesheet = builder.Build();
            workbookPart.WorkbookStylesPart.Stylesheet = stylesheet;
            var formatCount = stylesheet.CellFormats?.Count?.Value ?? 1;

            var strings = new XlsStringTable();
            var writer = new XlsSheetWriter(strings, (int)formatCount);
            var sheets = workbook.Sheets;
            var firstPart = workbookPart.WorksheetParts.First();
            var sheetElements = new Sheets();
            var firstVisible = sheets.FindIndex(s => s.Info.State == 0);
            if (firstVisible < 0)
                firstVisible = 0;

            for (var i = 0; i < sheets.Count; i++)
            {
                var part = i == 0 ? firstPart : workbookPart.AddNewPart<WorksheetPart>();
                part.Worksheet = writer.Build(sheets[i], i == firstVisible);
                var element = new Sheet { Name = sheets[i].Info.Name, SheetId = (uint)i + 1, Id = workbookPart.GetIdOfPart(part) };
                if (i != firstVisible && sheets[i].Info.State == 1)
                    element.State = SheetStateValues.Hidden;
                else if (i != firstVisible && sheets[i].Info.State == 2)
                    element.State = SheetStateValues.VeryHidden;

                sheetElements.Append(element);
            }

            if (sheets.Count == 0)
            {
                firstPart.Worksheet = new Worksheet(new SheetViews(new SheetView { TabSelected = true, WorkbookViewId = 0 }), new SheetFormatProperties { DefaultRowHeight = 15 }, new SheetData());
                sheetElements.Append(new Sheet { Name = BlankWorkbook.FirstSheetName, SheetId = 1, Id = workbookPart.GetIdOfPart(firstPart) });
            }

            workbookPart.SharedStringTablePart.SharedStringTable = strings.Build();

            var root = new Workbook();
            if (workbook.Date1904)
                root.Append(new WorkbookProperties { Date1904 = true });

            root.Append(new BookViews(new WorkbookView { ActiveTab = (uint)firstVisible }));
            root.Append(sheetElements);
            var names = BuildNames(workbook.Names);
            if (names != null)
                root.Append(names);

            root.Append(new CalculationProperties { FullCalculationOnLoad = true });
            workbookPart.Workbook = root;
        }

        private static DefinedNames BuildNames(List<XlsDefinedName> names)
        {
            if (names.Count == 0)
                return null;

            var result = new DefinedNames();
            foreach (var name in names)
            {
                if (!XlsXmlText.IsClean(name.Formula) || !XlsXmlText.IsClean(name.Name))
                    continue;

                var element = new DefinedName(name.Formula) { Name = name.Name };
                if (name.LocalSheet != null)
                    element.LocalSheetId = (uint)name.LocalSheet.Value;

                if (name.Hidden)
                    element.Hidden = true;

                result.Append(element);
            }

            return result.HasChildren ? result : null;
        }
    }
}
