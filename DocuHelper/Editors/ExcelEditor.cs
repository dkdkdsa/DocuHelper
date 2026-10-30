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
