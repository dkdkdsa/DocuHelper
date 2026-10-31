using DocuHelper.Docus;
using DocuHelper.Tools.Excel;

namespace DocuHelper.Editors
{
    public class ExcelEditor : DocuEditor
    {
        public ExcelEditor()
        {
            AddTool(new ReadSheetTool());
            AddTool(new WriteCellsTool());
            AddTool(new SetFormulaTool());
            AddTool(new FormatRangeTool());
            AddTool(new SortRangeTool());
            AddTool(new AddSheetTool());
            AddTool(new InsertRowsColumnsTool());
            AddTool(new DeleteRowsColumnsTool());
            AddTool(new FormatRowsColumnsTool());
            AddTool(new MergeCellsTool());
            AddTool(new FreezePanesTool());
            AddTool(new ChangeSheetTool());
            AddTool(new CopySheetTool());
            AddTool(new DeleteSheetTool());
            AddTool(new ManageTableTool());
            AddTool(new FilterRangeTool());
            AddTool(new RemoveDuplicatesTool());
            AddTool(new SetConditionalFormatTool());
            AddTool(new CopyRangeTool());
            AddTool(new ShiftCellsTool());
            AddTool(new SetDataValidationTool());
            AddTool(new SetHyperlinkTool());
            AddTool(new EditCommentTool());
            AddTool(new SetSheetViewTool());
            AddTool(new SetWorkbookOptionsTool());
            AddTool(new DefineNameTool());
            AddTool(new SetPrintSetupTool());
            AddTool(new ProtectTool());
        }

        public override DocuType TargetType => DocuType.Excel;

        public override string Extension => ".xlsx";

        public override string Describe(Docu docu)
        {
            if (docu is not ExcelDocu excel || excel.Document?.WorkbookPart?.Workbook == null)
                return docu.Name;

            return WorkbookSummary.Describe(new ExcelBook(excel.Document));
        }

        public override Docu CreateBlank(string filePath) => new ExcelDocu(filePath, BlankWorkbook.Create());
    }
}
