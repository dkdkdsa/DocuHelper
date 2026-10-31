using System;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    internal static class WorksheetOrder
    {
        private static readonly string[] s_order =
        {
            "sheetPr", "dimension", "sheetViews", "sheetFormatPr", "cols", "sheetData", "sheetCalcPr", "sheetProtection", "protectedRanges",
            "scenarios", "autoFilter", "sortState", "dataConsolidate", "customSheetViews", "mergeCells", "phoneticPr", "conditionalFormatting",
            "dataValidations", "hyperlinks", "printOptions", "pageMargins", "pageSetup", "headerFooter", "rowBreaks", "colBreaks",
            "customProperties", "cellWatches", "ignoredErrors", "smartTags", "drawing", "legacyDrawing", "legacyDrawingHF", "drawingHF",
            "picture", "oleObjects", "controls", "webPublishItems", "tableParts", "extLst",
        };

        public static T Insert<T>(Worksheet worksheet, T element) where T : OpenXmlElement
        {
            var rank = Rank(element.LocalName);
            var after = worksheet.ChildElements.LastOrDefault(c => Rank(c.LocalName) <= rank);
            if (after != null)
                after.InsertAfterSelf(element);
            else
                worksheet.PrependChild(element);

            return element;
        }

        private static int Rank(string name)
        {
            var index = Array.IndexOf(s_order, name);
            return index < 0 ? s_order.Length : index;
        }
    }
}
