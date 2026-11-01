using System.Linq;
using DocuHelper.Tools.Excel;
using DocuHelperEditor.State.Papers;
using XlSheetProtection = DocumentFormat.OpenXml.Spreadsheet.SheetProtection;

namespace DocuHelperEditor.State.Mapping
{
    public partial class ExcelPaperMapper
    {
        static partial void MapProtection(ExcelFeatureContext context)
        {
            var protection = context.Source.Worksheet?.GetFirstChild<XlSheetProtection>();
            if (protection == null)
                return;

            context.View.Protection = new SheetProtection
            {
                IsProtected = protection.Sheet?.Value == true,
                HasPassword = protection.Password?.Value != null,
                SelectLockedCells = protection.SelectLockedCells?.Value != true,
                SelectUnlockedCells = protection.SelectUnlockedCells?.Value != true,
                AllowFormatCells = protection.FormatCells?.Value == false,
                AllowFormatColumns = protection.FormatColumns?.Value == false,
                AllowFormatRows = protection.FormatRows?.Value == false,
                AllowInsertColumns = protection.InsertColumns?.Value == false,
                AllowInsertRows = protection.InsertRows?.Value == false,
                AllowInsertHyperlinks = protection.InsertHyperlinks?.Value == false,
                AllowDeleteColumns = protection.DeleteColumns?.Value == false,
                AllowDeleteRows = protection.DeleteRows?.Value == false,
                AllowSort = protection.Sort?.Value == false,
                AllowAutoFilter = protection.AutoFilter?.Value == false,
                AllowPivotTables = protection.PivotTables?.Value == false,
                AllowObjects = protection.Objects?.Value == false,
                AllowScenarios = protection.Scenarios?.Value == false,
            };
        }
    }
}
