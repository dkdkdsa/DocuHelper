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
            AddDisabled("excel.file.info", PendingReason);
            AddExternal("excel.file.openexcel");
            AddUndo("excel.trailing.undo");
            AddRedo("excel.trailing.redo");
        }
    }
}
