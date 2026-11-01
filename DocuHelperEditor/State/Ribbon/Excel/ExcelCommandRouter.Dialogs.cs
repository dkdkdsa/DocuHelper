namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter
    {
        private void RegisterDialogs()
        {
            State.RegisterDialog(ExcelDialogIds.ConditionalFormat);
            State.RegisterDialog(ExcelDialogIds.Validation);
            State.RegisterDialog(ExcelDialogIds.ProtectSheet);
            foreach (var id in ExcelNamedDialogs.Ids)
                State.RegisterDialog(id);
        }
    }
}
