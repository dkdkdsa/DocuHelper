using System.Linq;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter
    {
        private const string PrintPendingReason = "인쇄는 아직 준비 중이에요.";

        private void RegisterFile()
        {
            AddSave("excel.file.save");
            AddSaveCopy("excel.file.saveas");
            AddDisabled("excel.file.pdf", PrintPendingReason);
            AddDisabled("excel.file.print", PrintPendingReason);
            AddFile("excel.file.info", _ => ShowWorkbookInfo());
            AddExternal("excel.file.openexcel");
            AddUndo("excel.trailing.undo");
            AddRedo("excel.trailing.redo");
        }

        private void ShowWorkbookInfo()
        {
            if (File == null || State.Model == null)
            {
                Notify(NoFileReason);
                return;
            }

            var sheets = State.Model.Sheets;
            var active = State.Sheet;
            var names = string.Join(", ", sheets.Select(sheet => sheet.Name));
            var tables = sheets.Sum(sheet => sheet.Tables.Count);
            var filled = active.Rows.Sum(row => row.Cells.Count(cell => cell != null && !string.IsNullOrEmpty(cell.Text)));
            Notify($"‘{File.Name}’에는 시트가 {sheets.Count}개 있어요: {names}. 지금 시트 {active.Name}에는 값이 있는 칸이 {filled:N0}개 있고, 표는 모두 {tables}개예요.");
        }
    }
}
